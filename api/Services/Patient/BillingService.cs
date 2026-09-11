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
using BedAssignmentEntity = CareLanka.Api.Data.Entities.Patient.BedAssignment;
using BillEntity = CareLanka.Api.Data.Entities.Patient.Bill;
using BillLineEntity = CareLanka.Api.Data.Entities.Patient.BillLineItem;
using BillResponse = CareLanka.Api.DTOs.Patient.Bill;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class BillingService : IBillingService
{
    /// <summary>
    /// A visit whose money could still be outstanding. A discharged one cannot be: confirming a
    /// discharge needs <c>billing_settled</c>, which only settling the bill writes.
    /// </summary>
    private static readonly AdmissionStatus[] StillOwing =
        [AdmissionStatus.Admitted, AdmissionStatus.ReadyForDischarge];

    /// <summary>How many bill numbers to draw before giving up. See <see cref="NextBillNumberAsync"/>.</summary>
    private const int NumberAttempts = 5;

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly IDischargeService _discharges;
    private readonly ICurrentUser _currentUser;

    public BillingService(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IDischargeService discharges,
        ICurrentUser currentUser)
    {
        _db = db;
        _beds = beds;
        _discharges = discharges;
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
            .Include(row => row.Admission)
                .ThenInclude(admission => admission.Patient)
            .FirstOrDefaultAsync(row => row.AdmissionId == admissionId, ct);

        return bill is null ? null : ToResponse(bill, bill.Admission.Patient);
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
            // A bill written for the first time by somebody adding a charge still gets its
            // stay lines, so the total is never "one X-ray and no bed".
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

                // Not null: [ApiController] has already returned a 400 for a body that left
                // either out. Nullable on the request so omission is an error and not a zero.
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
                // A bed or fee line is worked out from the stay, so deleting it would be a
                // number that comes back the next time anyone prepares the bill.
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
            // Settling a visit nobody prepared a bill for works out the bill first. Without
            // this a visit with no bill row could never tick billing_settled, and therefore
            // could never be discharged at all - a deadlock with no way out for the desk.
            if (bill.LineItems.Count == 0)
            {
                await RegenerateAsync(admission, bill, ct);
            }

            bill.SettledAt = DateTimeOffset.UtcNow;
            bill.SettledByStaffMemberId = _currentUser.Id;
            bill.SettlementNote = Clean(request.SettlementNote);

            // The one write of billing_settled in the whole component. It joins this
            // transaction, so a settled bill with an unticked box cannot exist.
            await _discharges.MarkBillingSettledAsync(admissionId, true, ct);
        }, ct);

    public async Task<PagedResult<OutstandingBill>> ListOutstandingAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var query = _db.Admissions
            .AsNoTracking()
            .Include(admission => admission.Patient)
            .Include(admission => admission.BedAssignments)
            .Where(admission => StillOwing.Contains(admission.Status))

            // No bill row at all is the common case - a bill is written the first time somebody
            // asks for one - so a list of bills would have shown reception an empty screen and
            // left the work invisible.
            .Where(admission => !_db.Bills.Any(bill =>
                bill.AdmissionId == admission.Id && bill.SettledAt != null));

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
            .Where(bill => admissionIds.Contains(bill.AdmissionId))
            .ToDictionaryAsync(bill => bill.AdmissionId, ct);

        var labels = await BedLabels.LiveByAdmissionAsync(_db, _beds, admissionIds, now, ct);
        var wardTypes = await WardTypesByBedAsync(admissions, ct);

        var rows = new List<OutstandingBill>();

        foreach (var admission in admissions)
        {
            var label = labels.TryGetValue(admission.Id, out var found) ? found : BedLabel.Unknown;

            // What it comes to as it stands: the prepared total if there is one, otherwise what
            // preparing it now would produce. Advisory - the bill screen is what writes lines.
            var estimated = bills.TryGetValue(admission.Id, out var bill) && bill.LineItems.Count > 0
                ? bill.LineItems.Sum(line => line.LineTotal)
                : GenerateLines(admission, Guid.Empty, wardTypes, labels: null, now)
                    .Sum(line => line.LineTotal);

            rows.Add(new OutstandingBill
            {
                AdmissionId = admission.Id,
                Patient = ToPatientSummary(admission.Patient),
                Status = admission.Status,
                AdmissionCategory = admission.Category,
                WardName = label.WardName,
                BedNumber = label.BedNumber,
                AdmittedAt = admission.AdmittedAt,
                BillNumber = bill?.BillNumber,
                EstimatedTotal = decimal.Round(estimated, 2),
                Currency = BillingRates.Currency
            });
        }

        return PagedResult<OutstandingBill>.From(rows, page, pageSize, totalItems);
    }

    // ---------- the write path ----------

    /// <summary>
    /// Every write goes through here: lock the admission, find or open its bill, refuse if it
    /// is settled, do the work, save and commit.
    /// </summary>
    /// <remarks>
    /// The row lock is the same one <c>/arrive</c>, <c>/cancel</c>, <c>assign-bed</c> and
    /// <c>confirm</c> take. It matters most on settle: settling and confirming a discharge both
    /// read the same admission, and without the lock a discharge could be confirmed between the
    /// bill being settled and the checklist tick landing.
    /// </remarks>
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
            // Frozen. A settled bill is what the patient was handed at the counter, and a line
            // added afterwards would make the paper and the database disagree.
            throw new ConflictException(MessageCode.BillAlreadySettled, bill.BillNumber);
        }

        await work(admission, bill);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToResponse(bill, admission.Patient);
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
            BillNumber = await NextBillNumberAsync(ct)
        };

        _db.Bills.Add(bill);

        return bill;
    }

    /// <summary>
    /// Replaces every line this component worked out for itself, and leaves every typed one
    /// alone. Preparing the bill again a day later therefore updates the bed days and keeps the
    /// X-ray reception entered.
    /// </summary>
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

        foreach (var line in GenerateLines(
            admission, bill.Id, wardTypes, labels, DateTimeOffset.UtcNow))
        {
            Add(bill, line);
        }
    }

    /// <summary>
    /// Puts a new line on a bill, telling EF in both directions that it is new.
    /// </summary>
    /// <remarks>
    /// The explicit <c>_db.BillLineItems.Add</c> is not belt and braces. We allocate the key
    /// ourselves, and change tracking decides Added-versus-Modified for an entity it meets
    /// through a navigation by looking at the key: a non-default one means "this row already
    /// exists", so the line is saved as an UPDATE that matches no row and the request dies with
    /// a concurrency exception. Adding it to its <c>DbSet</c> says outright that it is new.
    /// </remarks>
    private void Add(BillEntity bill, BillLineEntity line)
    {
        bill.LineItems.Add(line);
        _db.BillLineItems.Add(line);
    }

    /// <summary>
    /// The whole pricing rule, in one method, priced entirely from facts this component stores.
    /// </summary>
    /// <remarks>
    /// Two kinds of line and no others:
    ///
    /// **The admission fee**, from <c>Admission.Category</c> - the care level a named clinician
    /// chose and signed for.
    ///
    /// **A bed, per day, per assignment.** One line for each bed the patient has actually been
    /// in, so a transfer mid-stay bills each ward at its own rate. A bed that was only ever held
    /// and never slept in is not billed: nobody was in it.
    ///
    /// There is no third kind, and inventing one would mean inventing data. Nothing in this
    /// schema records a treatment, a procedure, a scan or a drug against an admission -
    /// Equipment's <c>PharmacyTransaction</c> has no admission column and its stock movements
    /// are not attributable to a patient. That is why reception types the rest by hand, and why
    /// patient-management-plan.md says so out loud rather than faking it.
    /// </remarks>
    private static IReadOnlyList<BillLineEntity> GenerateLines(
        AdmissionEntity admission,
        Guid billId,
        IReadOnlyDictionary<Guid, WardType> wardTypesByBedId,
        IReadOnlyDictionary<Guid, BedLabel>? labels,
        DateTimeOffset now)
    {
        var lines = new List<BillLineEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BillId = billId,
                Source = BillLineSource.AdmissionFee,
                Description = $"Admission fee ({EnumWire.ToWire(admission.Category)})",
                Quantity = 1m,
                UnitPrice = BillingRates.AdmissionFee(admission.Category)
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
                UnitPrice = BillingRates.BedDay(wardType),
                BedAssignmentId = stay.Id
            });
        }

        return lines;
    }

    /// <summary>
    /// Was the patient ever actually in this bed? A hold that lapsed or was cancelled is not a
    /// stay, and billing one would charge somebody for a bed they never saw.
    /// </summary>
    private static bool WasSleptIn(BedAssignmentEntity assignment)
        => assignment.OccupiedAt is not null
           || assignment.Status == AssignmentStatus.Occupied
           || (assignment.Status == AssignmentStatus.Released
               && assignment.ReleaseReason is ReleaseReason.Discharged or ReleaseReason.Transferred);

    /// <summary>
    /// When the stay started. <c>OccupiedAt</c> is the honest answer; <c>CreatedAt</c> is the
    /// fallback for rows written before that column existed, and is at most the length of a
    /// hold out.
    /// </summary>
    private static DateTimeOffset StartOf(BedAssignmentEntity assignment)
        => assignment.OccupiedAt ?? assignment.CreatedAt;

    private static string Describe(BedLabel label, WardType? wardType)
    {
        if (string.IsNullOrEmpty(label.BedNumber))
        {
            // The bed has been retired from Equipment's register since the stay. The stay is
            // still real and still billable; we just cannot name the bed any more.
            return "Bed stay";
        }

        var ward = string.IsNullOrEmpty(label.WardName) ? "ward withdrawn" : label.WardName;

        return wardType is { } type
            ? $"Bed {label.BedNumber}, {ward} ({EnumWire.ToWire(type)})"
            : $"Bed {label.BedNumber}, {ward}";
    }

    /// <summary>
    /// The ward type behind each bed these admissions have used, for the day rate.
    /// </summary>
    /// <remarks>
    /// <c>IgnoreQueryFilters</c> on purpose, which is rare in this component. A ward retired
    /// last month still has to price a stay that happened while it was open - the global filter
    /// would hide it, the rate would fall back to general, and a patient who was in intensive
    /// care would be undercharged by nineteen thousand rupees a day.
    /// </remarks>
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

    /// <summary>
    /// A bill number nothing else is using.
    /// </summary>
    /// <remarks>
    /// Checked with a read rather than by catching the unique index, because the insert happens
    /// inside a transaction and a failed <c>SaveChanges</c> there aborts the whole thing - there
    /// would be nothing left to retry into. At 31^7 candidates a collision is not something that
    /// happens; the index is still the guarantee, this loop is only politeness.
    /// </remarks>
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

    // ---------- mapping ----------

    private static BillResponse ToResponse(BillEntity bill, PatientEntity patient)
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
            BillNumber = bill.BillNumber,
            Currency = BillingRates.Currency,
            Lines = lines,

            // The sum of what is printed, worked out from the same list the patient is looking
            // at. Not a column, so it cannot drift away from the lines it came from.
            Total = decimal.Round(lines.Sum(line => line.LineTotal), 2),
            Settled = bill.IsSettled,
            SettledAt = bill.SettledAt,
            SettledByStaffId = bill.SettledByStaffMemberId,
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
