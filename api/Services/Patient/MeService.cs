using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AppointmentEntity = CareLanka.Api.Data.Entities.Patient.Appointment;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>Everything a patient may do about themselves from the app. See <see cref="IMeService"/>.</summary>
public sealed class MeService : IMeService
{
    // A visit that has ended. The same two statuses AdmissionService and AppointmentService
    // treat as closed, so "your current stay" and the ward board never disagree about whether
    // somebody is still here.
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

    public MeService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        IPatientService patients,
        IAppointmentService appointments,
        IBedRegistryService beds)
    {
        _db = db;
        _currentUser = currentUser;
        _patients = patients;
        _appointments = appointments;
        _beds = beds;
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

    public async Task<MyProfile> GetProfileAsync(CancellationToken ct = default)
        => ToProfile(await GetMyRecordAsync(ct));

    public async Task<MyAdmission> GetCurrentAdmissionAsync(CancellationToken ct = default)
    {
        var patient = await GetMyRecordAsync(ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .Where(a => a.PatientId == patient.Id && !ClosedStatuses.Contains(a.Status))
            // At most one open admission per patient - ux_admissions_open_patient makes that a
            // database guarantee. Ordered anyway so that if the index is ever relaxed this
            // returns the newest rather than an arbitrary one.
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(MessageCode.NoCurrentAdmission);

        var beds = await BedLabels.CurrentOrLastByAdmissionAsync(
            _db, _beds, [admission.Id], DateTimeOffset.UtcNow, ct);

        var instructions = await InstructionsByAdmissionAsync([admission.Id], ct);

        return ToMyAdmission(admission, beds, instructions);
    }

    public async Task<PagedResult<MyAdmission>> GetHistoryAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var patient = await FindMyRecordAsync(ct);

        if (patient is null)
        {
            // An empty page, not a 404. Somebody who signed up last night and has never been
            // treated here has no history, and that is an ordinary answer to the question - the
            // screen shows "no past visits", which is true.
            return PagedResult<MyAdmission>.From([], page, pageSize, 0);
        }

        // Finished visits only. The one that is still open is GET /me/admission, and listing it
        // in both places would show a patient their current stay twice on one screen.
        var query = _db.Admissions
            .AsNoTracking()
            .Where(a => a.PatientId == patient.Id && ClosedStatuses.Contains(a.Status));

        var totalItems = await query.CountAsync(ct);

        var admissions = await query
            // Most recent first: a history screen is read from the top. Id breaks ties so a row
            // cannot appear on two pages while another appears on none.
            .OrderByDescending(a => a.DischargedAt ?? a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = admissions.Select(a => a.Id).ToList();

        // Two queries for the page, not two per row. CurrentOrLastByAdmissionAsync and not
        // LiveByAdmissionAsync: a discharge releases the bed, so asking where they are now
        // would answer "no bed" about somebody who spent three days in GEN-02.
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
            // A login is not a medical record. Filling in the details form is what joins the
            // two, and until it has been done there is nobody to book the visit for.
            ?? throw new ConflictException(MessageCode.AccountHasNoPatientRecord);

        // Through the appointment service, not by building the entity here. The future-date
        // rule, the one-open-booking rule and the not-while-admitted rule all live there, and a
        // self-booking has to obey every one of them exactly as a desk booking does.
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
            // Newest booking first. The one a patient cares about is the next visit, and on a
            // list that is almost always the most recently made one. Id breaks ties so a row
            // cannot appear on two pages while another appears on none.
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

        // Both ids go in together. The service matches on the pair, so an appointment belonging
        // to somebody else reads as one that does not exist.
        var appointment = await _appointments.CancelForPatientAsync(appointmentId, patient.Id, ct);

        return ToMyAppointment(appointment);
    }

    /// <summary>
    /// Their own record, which they may correct in full - except for which human it belongs to.
    /// </summary>
    /// <remarks>
    /// A null in the body means "not filled in on this screen", never "clear what is stored".
    /// A patient part-way through the form would otherwise wipe the address the desk typed in
    /// for them last week. There is no way to blank a field from the app, and no need for one:
    /// a wrong address is replaced with the right one.
    /// </remarks>
    private async Task<MyProfile> UpdateOwnRecordAsync(
        PatientEntity mine, string nic, PreRegisterRequest request, CancellationToken ct)
    {
        if (mine.Nic is not null
            && !string.Equals(mine.Nic, nic, StringComparison.OrdinalIgnoreCase))
        {
            // Which human this login belongs to is not something the app may change. Somebody
            // whose NIC was typed in wrong at the desk has to have it corrected at the desk,
            // by a person who can look at the card.
            throw new ConflictException(MessageCode.NicDoesNotMatchYourRecord);
        }

        if (mine.Nic is null)
        {
            // Registered as an unidentified arrival and now supplying papers. Allowed, but not
            // if it would make two records the same person.
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

    /// <summary>
    /// A record the hospital already holds, matched on NIC. Linked to this login, and filled in
    /// only where it is blank.
    /// </summary>
    /// <remarks>
    /// <b>Nothing already on the record is overwritten, and that is a safety rule rather than a
    /// nicety.</b> Nothing here proves the person typing the NIC owns it. Linking is bad enough
    /// if they do not; letting them rewrite a stranger's name, date of birth and emergency
    /// contact would be a good deal worse. A production system puts an OTP or a desk check in
    /// front of this endpoint - written up in RESUME.md as the known gap.
    /// </remarks>
    private async Task<MyProfile> LinkExistingRecordAsync(
        PatientEntity existing, Guid accountId, PreRegisterRequest request, CancellationToken ct)
    {
        if (existing.UserAccountId is not null)
        {
            // Says nothing about whose record it is, or whether that person has a login. The
            // fix is the same either way: go to the desk, where somebody can see the card.
            throw new ConflictException(MessageCode.NicLinkedToAnotherAccount);
        }

        existing.DateOfBirth ??= request.DateOfBirth;
        existing.Phone ??= Clean(request.Phone);
        existing.Address ??= Clean(request.Address);
        existing.EmergencyContactName ??= Clean(request.EmergencyContactName);
        existing.EmergencyContactPhone ??= Clean(request.EmergencyContactPhone);

        // Through the patient service, which owns the account link and the unique index behind
        // it. Its own SaveChanges commits the field changes above in the same round trip.
        await _patients.LinkAccountAsync(existing.Id, accountId, ct);

        return ToProfile(existing);
    }

    /// <summary>Nobody by that NIC, so this is a new person: create the record and link it.</summary>
    private async Task<MyProfile> CreateAndLinkRecordAsync(
        Guid accountId, string nic, PreRegisterRequest request, CancellationToken ct)
    {
        // One transaction over both writes. A record created and then not linked is invisible to
        // the patient who just created it and a duplicate waiting to happen at the desk - and
        // the NIC unique index would refuse their second attempt at the form.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Through the patient service so a self-registration gets the same patient code, the
        // same NIC duplicate handling and the same identifier rules as one typed at the desk.
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

    /// <summary>The medical record behind this login, or null when nobody has linked one.</summary>
    /// <remarks>
    /// Null is the ordinary state for an account that signed up last night, not an error. Which
    /// of the two it is depends on what was asked - a history screen shows an empty list, a
    /// booking cannot go ahead - so this is a <c>Find</c> and the caller decides.
    /// </remarks>
    private Task<PatientEntity?> FindMyRecordAsync(CancellationToken ct)
        => _db.Patients.FirstOrDefaultAsync(p => p.UserAccountId == _currentUser.Id, ct);

    private async Task<PatientEntity> GetMyRecordAsync(CancellationToken ct)
        => await FindMyRecordAsync(ct)
        ?? throw new NotFoundException(MessageCode.AccountHasNoPatientRecord);

    /// <summary>
    /// What the ward wrote for these patients to read on the way out, by admission id. An
    /// admission nobody has discharged yet contributes no entry.
    /// </summary>
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

            // Empty means Equipment's register no longer has the bed, or the ward has been
            // retired. Published as null rather than "": there is nothing to show, and an empty
            // string renders as a blank line where the app would otherwise hide the row.
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

    private static MyAppointment ToMyAppointment(AppointmentEntity appointment)
        => new()
        {
            AppointmentId = appointment.Id,
            ScheduledAt = appointment.ScheduledAt,
            Status = appointment.Status,
            StatusText = PatientStatusText.For(appointment.Status),
            Reason = appointment.Reason,

            // The same rule the cancel endpoint enforces, published so the app hides the button
            // rather than offering one the server will refuse.
            CanCancel = appointment.Status == AppointmentStatus.Scheduled
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

            // Counted from the list rather than stored alongside it, so the two cannot disagree.
            DetailsComplete = missing.Count == 0,
            MissingFields = missing
        };
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
