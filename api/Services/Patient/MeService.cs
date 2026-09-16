using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Equipment;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AppointmentEntity = CareLanka.Api.Data.Entities.Patient.Appointment;
using BillEntity = CareLanka.Api.Data.Entities.Patient.Bill;
using LabReportDto = CareLanka.Api.DTOs.Equipment.LabReport;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class MeService : IMeService
{
    private static readonly AdmissionStatus[] ClosedStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPatientService _patients;
    private readonly IAppointmentService _appointments;
    private readonly IBedRegistryService _beds;
    private readonly ILabReportService _labReports;

    public MeService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        IPatientService patients,
        IAppointmentService appointments,
        IBedRegistryService beds,
        ILabReportService labReports)
    {
        _db = db;
        _currentUser = currentUser;
        _patients = patients;
        _appointments = appointments;
        _beds = beds;
        _labReports = labReports;
    }

    public async Task<MyProfile> PreRegisterAsync(
        PreRegisterRequest request, CancellationToken ct = default)
    {
        var accountId = _currentUser.Id;
        var nic = request.Nic.Trim();

        var mine = await _db.Patients.FirstOrDefaultAsync(p => p.UserAccountId == accountId, ct);

        if (mine is not null)
        {
            return await UpdateOwnRecordAsync(mine, nic, request, ct);
        }

        var existing = await _db.Patients.FirstOrDefaultAsync(p => p.Nic == nic, ct);

        return existing is not null
            ? await LinkExistingRecordAsync(existing, accountId, request, ct)
            : await CreateAndLinkRecordAsync(accountId, nic, request, ct);
    }

    public async Task<PatientClaimPreview> PreviewClaimAsync(
        ClaimByPatientCodeRequest request, CancellationToken ct = default)
    {
        var patient = await FindClaimableAsync(request, ct);

        return new PatientClaimPreview
        {
            PatientCode = patient.PatientCode,
            MaskedFullName = MaskedIdentity.Name(patient.FullName),
            MaskedPhone = MaskedIdentity.Phone(patient.Phone)
        };
    }

    public async Task<MyProfile> ClaimAsync(
        ClaimByPatientCodeRequest request, CancellationToken ct = default)
    {
        var patient = await FindClaimableAsync(request, ct);

        await _patients.LinkAccountAsync(patient.Id, _currentUser.Id, ct);

        return ToProfile(patient);
    }

    public async Task<MyProfile> GetProfileAsync(CancellationToken ct = default)
        => ToProfile(await GetMyRecordAsync(ct));

    public async Task<MyBill> GetBillAsync(Guid admissionId, CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == admissionId && a.PatientId == patient.Id, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        var bill = await _db.Bills
            .AsNoTracking()
            .Include(b => b.LineItems)
            .FirstOrDefaultAsync(b => b.AdmissionId == admissionId, ct)
            ?? throw new NotFoundException(MessageCode.NoBillRaised);

        return ToMyBill(bill, ClosedStatuses.Contains(admission.Status));
    }

    public async Task<MyBill> GetAppointmentBillAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        var appointment = await _db.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);

        // Set means the visit ended in an admission, so the bill lives there instead -
        // same rule the staff billing endpoint enforces on the way in.
        if (appointment.AdmissionId is not null)
        {
            throw new NotFoundException(MessageCode.AppointmentBilledOnItsAdmission);
        }

        var bill = await _db.Bills
            .AsNoTracking()
            .Include(b => b.LineItems)
            .FirstOrDefaultAsync(b => b.AppointmentId == appointmentId, ct)
            ?? throw new NotFoundException(MessageCode.NoBillRaised);

        // An appointment bill is a one-off consultation charge, never a running total that
        // grows day over day like a stay's does, so it reads as final as soon as it exists.
        return ToMyBill(bill, isFinal: true);
    }

    public async Task<MyAdmission> GetCurrentAdmissionAsync(CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .Where(a => a.PatientId == patient.Id && !ClosedStatuses.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(MessageCode.NoCurrentAdmission);

        var beds = await BedLabels.CurrentOrLastByAdmissionAsync(
            _db, _beds, [admission.Id], DateTimeOffset.UtcNow, ct);

        var instructions = await InstructionsByAdmissionAsync([admission.Id], ct);

        return ToMyAdmission(admission, beds, instructions);
    }

    public async Task<PagedResult<MyLabReport>> GetLabReportsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        var reports = await _labReports.ListForPatientAsync(patient.Id, page, pageSize, ct);

        return PagedResult<MyLabReport>.From(
            reports.Items.Select(ToMyLabReport).ToList(), page, pageSize, reports.TotalItems);
    }

    public async Task<LabReportFile> GetLabReportFileAsync(Guid reportId, CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        return await _labReports.GetFileForPatientAsync(reportId, patient.Id, ct);
    }

    public async Task<PagedResult<MyAdmission>> GetHistoryAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var patient = await FindMyRecordAsync(ct);

        if (patient is null)
        {
            return PagedResult<MyAdmission>.From([], page, pageSize, 0);
        }

        var query = _db.Admissions
            .AsNoTracking()
            .Where(a => a.PatientId == patient.Id && ClosedStatuses.Contains(a.Status));

        var totalItems = await query.CountAsync(ct);

        var admissions = await query
            .OrderByDescending(a => a.DischargedAt ?? a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = admissions.Select(a => a.Id).ToList();

        var beds = await BedLabels.CurrentOrLastByAdmissionAsync(
            _db, _beds, ids, DateTimeOffset.UtcNow, ct);

        var instructions = await InstructionsByAdmissionAsync(ids, ct);

        return PagedResult<MyAdmission>.From(
            admissions.Select(a => ToMyAdmission(a, beds, instructions)).ToList(),
            page, pageSize, totalItems);
    }

    public async Task<MyAppointment> BookAppointmentAsync(
        BookAppointmentRequest request, CancellationToken ct = default)
    {
        var patient = await FindMyRecordAsync(ct)
            ?? throw new ConflictException(MessageCode.AccountHasNoPatientRecord);

        var appointment = await _appointments.BookForPatientAsync(patient.Id, request, ct);

        return ToMyAppointment(appointment);
    }

    public async Task<PagedResult<MyAppointment>> ListAppointmentsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var patient = await FindMyRecordAsync(ct);

        if (patient is null)
        {
            return PagedResult<MyAppointment>.From([], page, pageSize, 0);
        }

        var query = _db.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patient.Id);

        var totalItems = await query.CountAsync(ct);

        var appointments = await query
            .OrderByDescending(a => a.ScheduledAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<MyAppointment>.From(
            appointments.Select(ToMyAppointment).ToList(), page, pageSize, totalItems);
    }

    public async Task<MyAppointment> CancelAppointmentAsync(
        Guid appointmentId, CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        var appointment = await _appointments.CancelForPatientAsync(appointmentId, patient.Id, ct);

        return ToMyAppointment(appointment);
    }

    private async Task<MyProfile> UpdateOwnRecordAsync(
        PatientEntity mine, string nic, PreRegisterRequest request, CancellationToken ct)
    {
        if (mine.Nic is not null
            && !string.Equals(mine.Nic, nic, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(MessageCode.NicDoesNotMatchYourRecord);
        }

        if (mine.Nic is null)
        {
            await EnsureNicIsFreeAsync(nic, mine.Id, ct);

            mine.Nic = nic;
        }

        mine.FullName = request.FullName.Trim();
        mine.Gender = request.Gender!.Value;
        mine.DateOfBirth = request.DateOfBirth ?? mine.DateOfBirth;
        mine.Phone = Clean(request.Phone) ?? mine.Phone;
        mine.Address = Clean(request.Address) ?? mine.Address;
        mine.EmergencyContactName = Clean(request.EmergencyContactName) ?? mine.EmergencyContactName;
        mine.EmergencyContactPhone = Clean(request.EmergencyContactPhone) ?? mine.EmergencyContactPhone;

        await _db.SaveChangesAsync(ct);

        return ToProfile(mine);
    }

    private async Task<MyProfile> LinkExistingRecordAsync(
        PatientEntity existing, Guid accountId, PreRegisterRequest request, CancellationToken ct)
    {
        if (existing.UserAccountId is not null)
        {
            throw new ConflictException(MessageCode.NicLinkedToAnotherAccount);
        }

        existing.DateOfBirth ??= request.DateOfBirth;
        existing.Phone ??= Clean(request.Phone);
        existing.Address ??= Clean(request.Address);
        existing.EmergencyContactName ??= Clean(request.EmergencyContactName);
        existing.EmergencyContactPhone ??= Clean(request.EmergencyContactPhone);

        await _patients.LinkAccountAsync(existing.Id, accountId, ct);

        return ToProfile(existing);
    }

    private async Task<MyProfile> CreateAndLinkRecordAsync(
        Guid accountId, string nic, PreRegisterRequest request, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var created = await _patients.CreateAsync(new CreatePatientRequest
        {
            FullName = request.FullName,
            Nic = nic,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Phone = request.Phone,
            Address = request.Address,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone
        }, ct);

        await _patients.LinkAccountAsync(created.Id, accountId, ct);

        await transaction.CommitAsync(ct);

        var patient = await _patients.GetByIdAsync(created.Id, ct);

        return ToProfile(patient);
    }

    private async Task EnsureNicIsFreeAsync(string nic, Guid exceptPatientId, CancellationToken ct)
    {
        var taken = await _db.Patients
            .AnyAsync(p => p.Nic == nic && p.Id != exceptPatientId, ct);

        if (taken)
        {
            throw new ConflictException(MessageCode.NicLinkedToAnotherAccount);
        }
    }

    private async Task<PatientEntity> FindClaimableAsync(
        ClaimByPatientCodeRequest request, CancellationToken ct)
    {
        if (await FindMyRecordAsync(ct) is not null)
        {
            throw new ConflictException(MessageCode.AccountAlreadyLinked);
        }

        var code = request.PatientCode.Trim().ToUpperInvariant();
        var nic = request.Nic.Trim();

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.PatientCode == code, ct);

        // One message for every way this fails. A distinct "wrong NIC" would tell a stranger
        // holding the slip that the code is real, which is what the NIC is there to stop.
        if (patient is null
            || patient.UserAccountId is not null
            || patient.Nic is null
            || patient.Nic != nic)
        {
            throw new NotFoundException(MessageCode.PatientCodeNotClaimable);
        }

        return patient;
    }

    private Task<PatientEntity?> FindMyRecordAsync(CancellationToken ct)
        => _db.Patients.FirstOrDefaultAsync(p => p.UserAccountId == _currentUser.Id, ct);

    private async Task<PatientEntity> GetMyRecordAsync(CancellationToken ct)
        => await FindMyRecordAsync(ct)
        ?? throw new NotFoundException(MessageCode.AccountHasNoPatientRecord);

    private async Task<IReadOnlyDictionary<Guid, string>> InstructionsByAdmissionAsync(
        IReadOnlyCollection<Guid> admissionIds, CancellationToken ct)
    {
        if (admissionIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = admissionIds.ToList();

        return await _db.Discharges
            .AsNoTracking()
            .Where(d => ids.Contains(d.AdmissionId) && d.SummaryNote != null)
            .ToDictionaryAsync(d => d.AdmissionId, d => d.SummaryNote!, ct);
    }

    private static MyAdmission ToMyAdmission(
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> beds,
        IReadOnlyDictionary<Guid, string> instructions)
    {
        var label = beds.TryGetValue(admission.Id, out var found) ? found : (BedLabel?)null;

        return new MyAdmission
        {
            AdmissionId = admission.Id,
            Status = admission.Status,
            StatusText = PatientStatusText.For(admission.Status),

            WardName = NullIfEmpty(label?.WardName),
            BedNumber = NullIfEmpty(label?.BedNumber),

            AdmittedAt = admission.AdmittedAt,
            ExpectedArrival = admission.ExpectedArrivalAt,
            DischargedAt = admission.DischargedAt,
            DischargeInstructions = instructions.TryGetValue(admission.Id, out var note) ? note : null,
            DetailsComplete = admission.DetailsComplete,
            MissingFields = admission.MissingFields.ToList()
        };
    }

    private static MyBill ToMyBill(BillEntity bill, bool isFinal)
    {
        var lines = bill.LineItems
            .OrderBy(line => line.Source)
            .ThenBy(line => line.CreatedAt)
            .ThenBy(line => line.Description)
            .Select(line => new MyBillLine
            {
                Source = line.Source,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal
            })
            .ToList();

        return new MyBill
        {
            AdmissionId = bill.AdmissionId,
            AppointmentId = bill.AppointmentId,
            BillNumber = bill.BillNumber,
            Currency = BillingRates.Currency,
            Lines = lines,
            Total = decimal.Round(lines.Sum(line => line.LineTotal), 2),
            IsFinal = isFinal,
            Settled = bill.IsSettled,
            SettledAt = bill.SettledAt,
            UpdatedAt = bill.UpdatedAt
        };
    }

    private static MyAppointment ToMyAppointment(AppointmentEntity appointment)
        => new()
        {
            AppointmentId = appointment.Id,
            ScheduledAt = appointment.ScheduledAt,
            Status = appointment.Status,
            StatusText = PatientStatusText.For(
                appointment.Status, appointment.CancelledByStaffMemberId is not null),
            Reason = appointment.Reason,

            CanCancel = appointment.Status == AppointmentStatus.Scheduled,
            CancellationReason = appointment.CancellationReason,
            CancelledByHospital = appointment.CancelledByStaffMemberId is not null
        };

    private static MyLabReport ToMyLabReport(LabReportDto report) => new()
    {
        Id = report.Id,
        TestName = report.TestName,
        Summary = report.Summary,
        FileName = report.FileName,
        ContentType = report.ContentType,
        ByteSize = report.ByteSize,
        CreatedAt = report.CreatedAt
    };

    private static MyProfile ToProfile(PatientEntity patient)
    {
        var missing = PatientDetailChecklist.MissingFor(patient);

        return new MyProfile
        {
            PatientCode = patient.PatientCode,
            FullName = patient.FullName,
            Nic = patient.Nic,
            Gender = patient.Gender,
            DateOfBirth = patient.DateOfBirth,
            Phone = patient.Phone,
            Address = patient.Address,
            EmergencyContactName = patient.EmergencyContactName,
            EmergencyContactPhone = patient.EmergencyContactPhone,

            DetailsComplete = missing.Count == 0,
            MissingFields = missing
        };
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
