using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AppointmentEntity = CareLanka.Api.Data.Entities.Patient.Appointment;
using BedAssignmentEntity = CareLanka.Api.Data.Entities.Patient.BedAssignment;
using BillEntity = CareLanka.Api.Data.Entities.Patient.Bill;
using BillLineEntity = CareLanka.Api.Data.Entities.Patient.BillLineItem;
using BillResponse = CareLanka.Api.DTOs.Patient.Bill;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class BillingService : IBillingService
{
    private static readonly AdmissionStatus[] StillOwing =
        [AdmissionStatus.Admitted, AdmissionStatus.ReadyForDischarge];

    private const int NumberAttempts = 5;

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly IDischargeService _discharges;
    private readonly IBillingRateService _rates;
    private readonly ICurrentUser _currentUser;

    public BillingService(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IDischargeService discharges,
        IBillingRateService rates,
        ICurrentUser currentUser)
    {
        _db = db;
        _beds = beds;
        _discharges = discharges;
        _rates = rates;
        _currentUser = currentUser;
    }

    public async Task<BillResponse> GetAsync(Guid admissionId, CancellationToken ct = default)
        => await FindForAdmissionAsync(admissionId, ct)
           ?? throw new NotFoundException("Bill", admissionId);

    public async Task<BillResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken ct = default)
    {
        var bill = await _db.Bills
            .AsNoTracking()
            .Include(row => row.LineItems)
            .Include(row => row.Admission!)
                .ThenInclude(admission => admission.Patient)
            .FirstOrDefaultAsync(row => row.AdmissionId == admissionId, ct);

        return bill is null ? null : await ToResponseAsync(bill, bill.Admission!.Patient, ct);
    }

    public Task<BillResponse> PrepareAsync(Guid admissionId, CancellationToken ct = default)
        => InTransactionAsync(admissionId, async (admission, bill) =>
        {
            await RegenerateAsync(admission, bill, ct);
        }, ct);

    public Task<BillResponse> AddChargeAsync(
        Guid admissionId, AddBillChargeRequest request, CancellationToken ct = default)
        => InTransactionAsync(admissionId, async (admission, bill) =>
        {
            if (bill.LineItems.Count == 0)
            {
                await RegenerateAsync(admission, bill, ct);
            }

            Add(bill, new BillLineEntity
            {
                Id = Guid.NewGuid(),
                BillId = bill.Id,
                Source = BillLineSource.Manual,
                Description = request.Description.Trim(),

                Quantity = request.Quantity!.Value,
                UnitPrice = request.UnitPrice!.Value
            });
        }, ct);

    public Task<BillResponse> RemoveChargeAsync(
        Guid admissionId, Guid lineId, CancellationToken ct = default)
        => InTransactionAsync(admissionId, (_, bill) =>
        {
            var line = bill.LineItems.FirstOrDefault(item => item.Id == lineId)
                ?? throw new NotFoundException("BillLine", lineId);

            if (line.Source != BillLineSource.Manual)
            {
                throw new ConflictException(MessageCode.BillLineNotRemovable);
            }

            bill.LineItems.Remove(line);
            _db.BillLineItems.Remove(line);

            return Task.CompletedTask;
        }, ct);

    public Task<BillResponse> SettleAsync(
        Guid admissionId, SettleBillRequest request, CancellationToken ct = default)
        => InTransactionAsync(admissionId, async (admission, bill) =>
        {
            // Rebuilt on every settle: the lines were counted when the bill was raised, and the
            // patient may have slept more nights or changed bed since then.
            await RegenerateAsync(admission, bill, ct);

            bill.SettledAt = DateTimeOffset.UtcNow;
            bill.SettledByStaffMemberId = _currentUser.Id;
            bill.SettlementNote = Clean(request.SettlementNote);

            await _discharges.MarkBillingSettledAsync(admissionId, true, ct);
        }, ct);

    public async Task<BillResponse> GetForAppointmentAsync(
        Guid appointmentId, CancellationToken ct = default)
    {
        var bill = await _db.Bills
            .AsNoTracking()
            .Include(row => row.LineItems)
            .Include(row => row.Appointment!)
                .ThenInclude(appointment => appointment.Patient)
            .FirstOrDefaultAsync(row => row.AppointmentId == appointmentId, ct)
            ?? throw new NotFoundException("Bill", appointmentId);

        return await ToResponseAsync(bill, bill.Appointment!.Patient, ct);
    }

    public Task<BillResponse> PrepareForAppointmentAsync(
        Guid appointmentId, CancellationToken ct = default)
        => InAppointmentTransactionAsync(
            appointmentId, (_, bill) => RegenerateForAppointmentAsync(bill, ct), ct);

    public Task<BillResponse> AddAppointmentChargeAsync(
        Guid appointmentId, AddBillChargeRequest request, CancellationToken ct = default)
        => InAppointmentTransactionAsync(appointmentId, async (_, bill) =>
        {
            if (bill.LineItems.Count == 0)
            {
                await RegenerateForAppointmentAsync(bill, ct);
            }

            Add(bill, new BillLineEntity
            {
                Id = Guid.NewGuid(),
                BillId = bill.Id,
                Source = BillLineSource.Manual,
                Description = request.Description.Trim(),

                Quantity = request.Quantity!.Value,
                UnitPrice = request.UnitPrice!.Value
            });
        }, ct);

    public Task<BillResponse> RemoveAppointmentChargeAsync(
        Guid appointmentId, Guid lineId, CancellationToken ct = default)
        => InAppointmentTransactionAsync(appointmentId, (_, bill) =>
        {
            var line = bill.LineItems.FirstOrDefault(item => item.Id == lineId)
                ?? throw new NotFoundException("BillLine", lineId);

            if (line.Source != BillLineSource.Manual)
            {
                throw new ConflictException(MessageCode.BillLineNotRemovable);
            }

            bill.LineItems.Remove(line);
            _db.BillLineItems.Remove(line);

            return Task.CompletedTask;
        }, ct);

    public Task<BillResponse> SettleAppointmentAsync(
        Guid appointmentId, SettleBillRequest request, CancellationToken ct = default)
        => InAppointmentTransactionAsync(appointmentId, async (_, bill) =>
        {
            await RegenerateForAppointmentAsync(bill, ct);

            bill.SettledAt = DateTimeOffset.UtcNow;
            bill.SettledByStaffMemberId = _currentUser.Id;
            bill.SettlementNote = Clean(request.SettlementNote);

            // Nothing to tick on a discharge checklist: nobody was admitted, so
            // there is no admission for the discharge service to flag.
        }, ct);

    private async Task<BillResponse> InAppointmentTransactionAsync(
        Guid appointmentId,
        Func<AppointmentEntity, BillEntity, Task> work,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM appointments WHERE id = {appointmentId} FOR UPDATE", ct);

        var appointment = await _db.Appointments
            .Include(row => row.Patient)
            .FirstOrDefaultAsync(row => row.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);

        if (appointment.AdmissionId is not null)
        {
            throw new ConflictException(MessageCode.AppointmentBilledOnItsAdmission);
        }

        var bill = await FindOrOpenForAppointmentAsync(appointment, ct);

        if (bill.IsSettled)
        {
            throw new ConflictException(MessageCode.BillAlreadySettled, bill.BillNumber);
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new ConflictException(
                MessageCode.AppointmentNotBillable, EnumWire.ToWire(appointment.Status));
        }

        await work(appointment, bill);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await ToResponseAsync(bill, appointment.Patient, ct);
    }

    private async Task<BillEntity> FindOrOpenForAppointmentAsync(
        AppointmentEntity appointment, CancellationToken ct)
    {
        var existing = await _db.Bills
            .Include(bill => bill.LineItems)
            .FirstOrDefaultAsync(bill => bill.AppointmentId == appointment.Id, ct);

        if (existing is not null)
        {
            return existing;
        }

        var bill = new BillEntity
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            BillNumber = await NextBillNumberAsync(ct),

            RaisedByStaffMemberId = _currentUser.Id
        };

        _db.Bills.Add(bill);

        return bill;
    }

    /// <summary>
    /// An appointment bill carries one generated line, the consultation itself.
    /// No admission fee and no bed nights, because nobody was admitted.
    /// </summary>
    private async Task RegenerateForAppointmentAsync(BillEntity bill, CancellationToken ct)
    {
        var stale = bill.LineItems.Where(line => line.Source != BillLineSource.Manual).ToList();

        foreach (var line in stale)
        {
            bill.LineItems.Remove(line);
            _db.BillLineItems.Remove(line);
        }

        Add(bill, new BillLineEntity
        {
            Id = Guid.NewGuid(),
            BillId = bill.Id,
            Source = BillLineSource.ConsultationFee,
            Description = "Consultation fee",
            Quantity = 1m,
            UnitPrice = BillingRates.ConsultationFee
        });
    }

    public async Task<PagedResult<OutstandingBill>> ListOutstandingAsync(
        string? search, bool includeSettled, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var query = _db.Admissions
            .AsNoTracking()
            .Include(admission => admission.Patient)
            .Include(admission => admission.BedAssignments)
            .AsQueryable();

        if (includeSettled)
        {
            query = query.Where(admission =>
                _db.Bills.Any(bill => bill.AdmissionId == admission.Id));
        }
        else
        {
            query = query
                .Where(admission => StillOwing.Contains(admission.Status))

                .Where(admission => !_db.Bills.Any(bill =>
                    bill.AdmissionId == admission.Id && bill.SettledAt != null));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(admission =>
                EF.Functions.ILike(admission.Patient.FullName, $"%{term}%")
                || EF.Functions.ILike(admission.Patient.PatientCode, $"%{term}%")
                || (admission.Patient.Nic != null
                    && EF.Functions.ILike(admission.Patient.Nic, $"%{term}%")));
        }

        var totalItems = await query.CountAsync(ct);

        var admissions = await query
            .OrderBy(admission => admission.AdmittedAt ?? admission.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var admissionIds = admissions.Select(admission => admission.Id).ToList();

        var bills = await _db.Bills
            .AsNoTracking()
            .Include(bill => bill.LineItems)
            .Where(bill => bill.AdmissionId != null
                && admissionIds.Contains(bill.AdmissionId.Value))
            .ToDictionaryAsync(bill => bill.AdmissionId!.Value, ct);

        var labels = await BedLabels.LiveByAdmissionAsync(_db, _beds, admissionIds, now, ct);
        var wardTypes = await WardTypesByBedAsync(admissions, ct);

        var prices = await _rates.GetPriceListAsync(ct);

        var rows = new List<OutstandingBill>();

        foreach (var admission in admissions)
        {
            var label = labels.TryGetValue(admission.Id, out var found) ? found : BedLabel.Unknown;

            var estimated = bills.TryGetValue(admission.Id, out var bill) && bill.LineItems.Count > 0
                ? bill.LineItems.Sum(line => line.LineTotal)
                : GenerateLines(admission, Guid.Empty, wardTypes, labels: null, prices, now)
                    .Sum(line => line.LineTotal);

            rows.Add(new OutstandingBill
            {
                AdmissionId = admission.Id,
                Patient = ToPatientSummary(admission.Patient),
                Status = admission.Status,
                AdmissionCategory = admission.Category!.Value,
                WardName = label.WardName,
                BedNumber = label.BedNumber,
                AdmittedAt = admission.AdmittedAt,
                BillNumber = bill?.BillNumber,
                Settled = bill?.IsSettled ?? false,
                SettledAt = bill?.SettledAt,
                EstimatedTotal = decimal.Round(estimated, 2),
                Currency = BillingRates.Currency
            });
        }

        return PagedResult<OutstandingBill>.From(rows, page, pageSize, totalItems);
    }

    private async Task<BillResponse> InTransactionAsync(
        Guid admissionId,
        Func<AdmissionEntity, BillEntity, Task> work,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {admissionId} FOR UPDATE", ct);

        var admission = await _db.Admissions
            .Include(row => row.Patient)
            .Include(row => row.BedAssignments)
            .FirstOrDefaultAsync(row => row.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        var bill = await FindOrOpenAsync(admission, ct);

        if (bill.IsSettled)
        {
            throw new ConflictException(MessageCode.BillAlreadySettled, bill.BillNumber);
        }

        if (!StillOwing.Contains(admission.Status))
        {
            throw new ConflictException(
                MessageCode.BillNotOpenForStay, EnumWire.ToWire(admission.Status));
        }

        await work(admission, bill);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await ToResponseAsync(bill, admission.Patient, ct);
    }

    private async Task<BillEntity> FindOrOpenAsync(AdmissionEntity admission, CancellationToken ct)
    {
        var existing = await _db.Bills
            .Include(bill => bill.LineItems)
            .FirstOrDefaultAsync(bill => bill.AdmissionId == admission.Id, ct);

        if (existing is not null)
        {
            return existing;
        }

        var bill = new BillEntity
        {
            Id = Guid.NewGuid(),
            AdmissionId = admission.Id,
            BillNumber = await NextBillNumberAsync(ct),

            RaisedByStaffMemberId = _currentUser.Id
        };

        _db.Bills.Add(bill);

        return bill;
    }

    private async Task RegenerateAsync(AdmissionEntity admission, BillEntity bill, CancellationToken ct)
    {
        var stale = bill.LineItems.Where(line => line.Source != BillLineSource.Manual).ToList();

        foreach (var line in stale)
        {
            bill.LineItems.Remove(line);
            _db.BillLineItems.Remove(line);
        }

        var wardTypes = await WardTypesByBedAsync([admission], ct);

        var labels = await BedLabels.ByBedIdAsync(
            _db,
            _beds,
            admission.BedAssignments.Select(assignment => assignment.BedId).Distinct().ToList(),
            ct);

        var prices = await _rates.GetPriceListAsync(ct);

        foreach (var line in GenerateLines(
            admission, bill.Id, wardTypes, labels, prices, DateTimeOffset.UtcNow))
        {
            Add(bill, line);
        }
    }

    private void Add(BillEntity bill, BillLineEntity line)
    {
        bill.LineItems.Add(line);
        _db.BillLineItems.Add(line);
    }

    private static IReadOnlyList<BillLineEntity> GenerateLines(
        AdmissionEntity admission,
        Guid billId,
        IReadOnlyDictionary<Guid, WardType> wardTypesByBedId,
        IReadOnlyDictionary<Guid, BedLabel>? labels,
        PriceList prices,
        DateTimeOffset now)
    {
        var lines = new List<BillLineEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BillId = billId,
                Source = BillLineSource.AdmissionFee,
                Description = $"Admission fee ({EnumWire.ToWire(admission.Category!.Value)})",
                Quantity = 1m,
                UnitPrice = prices.AdmissionFee(admission.Category!.Value)
            }
        };

        var stays = admission.BedAssignments
            .Where(WasSleptIn)
            .OrderBy(assignment => StartOf(assignment))
            .ToList();

        foreach (var stay in stays)
        {
            var from = StartOf(stay);
            var to = stay.ReleasedAt ?? now;
            var days = BillingRates.BillableDays(from, to);

            var wardType = wardTypesByBedId.TryGetValue(stay.BedId, out var found)
                ? found
                : (WardType?)null;

            var label = labels is not null && labels.TryGetValue(stay.BedId, out var bed)
                ? bed
                : BedLabel.Unknown;

            lines.Add(new BillLineEntity
            {
                Id = Guid.NewGuid(),
                BillId = billId,
                Source = BillLineSource.BedStay,
                Description = Describe(label, wardType),
                Quantity = days,
                UnitPrice = prices.BedDay(wardType),
                BedAssignmentId = stay.Id
            });
        }

        return lines;
    }

    private static bool WasSleptIn(BedAssignmentEntity assignment)
        => assignment.ReleaseReason != ReleaseReason.Corrected
           && (assignment.OccupiedAt is not null
               || assignment.Status == AssignmentStatus.Occupied
               || (assignment.Status == AssignmentStatus.Released
                   && assignment.ReleaseReason is ReleaseReason.Discharged or ReleaseReason.Transferred));

    private static DateTimeOffset StartOf(BedAssignmentEntity assignment)
        => assignment.OccupiedAt ?? assignment.CreatedAt;

    private static string Describe(BedLabel label, WardType? wardType)
    {
        if (string.IsNullOrEmpty(label.BedNumber))
        {
            return "Bed stay";
        }

        var ward = string.IsNullOrEmpty(label.WardName) ? "ward withdrawn" : label.WardName;

        return wardType is { } type
            ? $"Bed {label.BedNumber}, {ward} ({EnumWire.ToWire(type)})"
            : $"Bed {label.BedNumber}, {ward}";
    }

    private async Task<IReadOnlyDictionary<Guid, WardType>> WardTypesByBedAsync(
        IReadOnlyCollection<AdmissionEntity> admissions, CancellationToken ct)
    {
        var bedIds = admissions
            .SelectMany(admission => admission.BedAssignments)
            .Select(assignment => assignment.BedId)
            .Distinct()
            .ToList();

        if (bedIds.Count == 0)
        {
            return new Dictionary<Guid, WardType>();
        }

        var beds = await _beds.ListBedsByIdAsync(bedIds, ct);

        var wardIds = beds.Select(bed => bed.WardId).Distinct().ToList();

        var wardTypes = await _db.Wards
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ward => wardIds.Contains(ward.Id))
            .ToDictionaryAsync(ward => ward.Id, ward => ward.WardType, ct);

        var result = new Dictionary<Guid, WardType>();

        foreach (var bed in beds)
        {
            if (wardTypes.TryGetValue(bed.WardId, out var type))
            {
                result[bed.Id] = type;
            }
        }

        return result;
    }

    private async Task<string> NextBillNumberAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= NumberAttempts; attempt++)
        {
            var candidate = BillCodes.Next();

            var taken = await _db.Bills
                .IgnoreQueryFilters()
                .AnyAsync(bill => bill.BillNumber == candidate, ct);

            if (!taken)
            {
                return candidate;
            }
        }

        throw new ConflictException(MessageCode.Conflict);
    }

    private async Task<BillResponse> ToResponseAsync(
        BillEntity bill, PatientEntity patient, CancellationToken ct)
    {
        var names = await StaffNames.ByIdAsync(
            _db, [bill.SettledByStaffMemberId, bill.RaisedByStaffMemberId], ct);

        return ToResponse(bill, patient, names);
    }

    private static BillResponse ToResponse(
        BillEntity bill, PatientEntity patient, IReadOnlyDictionary<Guid, string> names)
    {
        var lines = bill.LineItems
            .OrderBy(line => line.Source)
            .ThenBy(line => line.CreatedAt)
            .ThenBy(line => line.Description)
            .Select(line => new BillLine
            {
                Id = line.Id,
                Source = line.Source,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal
            })
            .ToList();

        return new BillResponse
        {
            Id = bill.Id,
            AdmissionId = bill.AdmissionId,
            AppointmentId = bill.AppointmentId,
            BillNumber = bill.BillNumber,
            Currency = BillingRates.Currency,
            Lines = lines,

            Total = decimal.Round(lines.Sum(line => line.LineTotal), 2),
            RaisedByStaffId = bill.RaisedByStaffMemberId,
            RaisedByStaffName = StaffNames.Lookup(names, bill.RaisedByStaffMemberId),
            Settled = bill.IsSettled,
            SettledAt = bill.SettledAt,
            SettledByStaffId = bill.SettledByStaffMemberId,
            SettledByStaffName = StaffNames.Lookup(names, bill.SettledByStaffMemberId),
            SettlementNote = bill.SettlementNote,
            Patient = ToPatientSummary(patient),
            CreatedAt = bill.CreatedAt,
            UpdatedAt = bill.UpdatedAt
        };
    }

    private static PatientSummary ToPatientSummary(PatientEntity patient)
        => new()
        {
            Id = patient.Id,
            PatientCode = patient.PatientCode,
            FullName = patient.FullName,
            Nic = patient.Nic,
            TempReference = patient.TempReference,
            Gender = patient.Gender,
            DateOfBirth = patient.DateOfBirth
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
