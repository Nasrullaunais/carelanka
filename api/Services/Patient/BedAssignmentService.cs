using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BedAssignmentEntity = CareLanka.Api.Data.Entities.Patient.BedAssignment;
using BedAssignmentResponse = CareLanka.Api.DTOs.Patient.BedAssignment;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

public sealed class BedAssignmentService : IBedAssignmentService
{
    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly IBedOccupancyService _occupancy;
    private readonly ICurrentUser _currentUser;

    public BedAssignmentService(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IBedOccupancyService occupancy,
        ICurrentUser currentUser)
    {
        _db = db;
        _beds = beds;
        _occupancy = occupancy;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AdmissionBed>> ListAvailabilityAsync(
        Guid? wardId,
        WardType? wardType,
        BedAvailabilityFilter availability,
        bool? needsIsolation,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // One instant for the whole page, taken once. Read the clock per bed and a hold can
        // lapse partway down the list, so two beds are judged against two different "nows".
        var now = DateTimeOffset.UtcNow;

        // The global query filter drops retired wards, so their beds never appear as
        // candidates. That is hard rule H5 arriving for free rather than as a second check.
        var wardQuery = _db.Wards.AsNoTracking();

        if (wardId is { } onlyWard)
        {
            wardQuery = wardQuery.Where(ward => ward.Id == onlyWard);
        }

        if (wardType is { } onlyType)
        {
            wardQuery = wardQuery.Where(ward => ward.WardType == onlyType);
        }

        var wards = await wardQuery.ToDictionaryAsync(ward => ward.Id, ward => ward.Name, ct);

        var beds = await _beds.ListBedsInWardsAsync(wards.Keys.ToList(), ct);

        if (needsIsolation is { } wantsIsolation)
        {
            beds = beds.Where(bed => bed.HasIsolation == wantsIsolation).ToList();
        }

        var claims = await ClaimsByBedAsync(beds.Select(bed => bed.Id).ToList(), now, ct);

        var candidates = beds
            .Select(bed => ToAdmissionBed(bed, wards[bed.WardId], claims))
            .Where(bed => Matches(bed.Availability, availability))

            // Ward, then bed number, so a bed board reads the way a ward is walked. Bed numbers
            // are Equipment's labels and not necessarily numeric, so this is a string sort.
            .OrderBy(bed => bed.WardName)
            .ThenBy(bed => bed.BedNumber)
            .ToList();

        // Paged in memory, unlike every other list in this component. "Free" is not a column
        // anywhere: it is Equipment's condition joined with our assignments and then judged
        // against the clock, so the rows have to exist before they can be counted. A hospital
        // has hundreds of beds, not millions, and the alternative is the expiry rule pushed
        // into SQL in a second place - the one thing patient-spec.yaml says not to do.
        return PagedResult<AdmissionBed>.From(
            candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page,
            pageSize,
            candidates.Count);
    }

    public async Task<BedAssignmentResponse> AssignManuallyAsync(
        Guid admissionId, AssignBedRequest request, CancellationToken ct = default)
    {
        // Not null: [ApiController] has already returned a 400 for a body that left it out.
        var bedId = request.BedId!.Value;

        var bed = await _beds.FindBedAsync(bedId, ct)

            // A 404 and not a 409: nothing about the admission is wrong, the caller named a bed
            // that is not in Equipment's register at all.
            ?? throw new NotFoundException("Bed", bedId);

        var ward = await FindWardAsync(bed.WardId, ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        // Refused on the rule before anything is written, so a nurse reaching for an ICU bed is
        // told why rather than stopped by some later side effect. Re-asked under the lock.
        EnsureMayApprove(admission.Category, ward);

        BedPlacementRules.EnsurePlaceable(
            admission.Category,
            admission.Patient.Gender,
            admission.Patient.DateOfBirth,
            admission.IsInfectious,
            ward,
            bed,
            IsDutyManager);

        var assignment = await WriteAsync(admissionId, bed, request.OverrideReason, ct);

        return await ToResponseAsync(assignment, ward!.Name, bed.BedNumber, ct);
    }

    public async Task<BedAssignmentResponse> CorrectBedAsync(
        Guid admissionId, CorrectBedRequest request, CancellationToken ct = default)
    {
        // Not null: [ApiController] has already returned a 400 for a body that left it out.
        var bedId = request.BedId!.Value;

        var bed = await _beds.FindBedAsync(bedId, ct)
            ?? throw new NotFoundException("Bed", bedId);

        var ward = await FindWardAsync(bed.WardId, ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        // Every rule the original placement had to pass, asked again on the replacement.
        // Correcting a bed is not a side door to a bed this person may not choose.
        EnsureMayApprove(admission.Category, ward);

        BedPlacementRules.EnsurePlaceable(
            admission.Category,
            admission.Patient.Gender,
            admission.Patient.DateOfBirth,
            admission.IsInfectious,
            ward,
            bed,
            IsDutyManager);

        var assignment = await SwapAsync(admissionId, bed, request.Reason, ct);

        return await ToResponseAsync(assignment, ward!.Name, bed.BedNumber, ct);
    }

    /// <summary>
    /// Closes the live assignment as a correction and opens a new one on the chosen bed, in one
    /// transaction, with the admission row locked.
    /// </summary>
    /// <remarks>
    /// <b>Two saves, not one, and that is the whole difficulty here.</b>
    /// <c>ux_bed_assignments_live_admission</c> allows one live row per admission and
    /// <c>ux_bed_assignments_live_bed</c> one per bed. PostgreSQL checks a unique index per
    /// statement, and a unique index cannot be deferred - so if EF happens to emit the INSERT
    /// of the new row before the UPDATE that releases the old one, the correction dies on an
    /// index whose rule was never actually broken. Releasing first, in its own
    /// <c>SaveChanges</c>, makes the order ours rather than EF's.
    ///
    /// <b>The status is carried over, not restarted.</b> A patient already in a bed stays
    /// <c>admitted</c> and the new row keeps the original <c>OccupiedAt</c>, so the bill is
    /// priced from when they actually got into a bed and not from when somebody noticed the
    /// paperwork was wrong. Correcting the bed of a patient who has not arrived leaves the hold
    /// a hold, with its original expiry.
    /// </remarks>
    private async Task<BedAssignmentEntity> SwapAsync(
        Guid admissionId,
        RegisteredBed bed,
        string? reason,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {admissionId} FOR UPDATE", ct);

        var admission = await _db.Admissions
            .Include(candidate => candidate.BedAssignments)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        var live = admission.BedAssignments
            .FirstOrDefault(assignment => assignment.Status != AssignmentStatus.Released)

            // Not a 404: the admission is real and so is the bed. There is simply nothing to
            // correct, and what this caller wants is /assign-bed.
            ?? throw new ConflictException(MessageCode.BedNotAssigned);

        if (live.BedId == bed.Id)
        {
            throw new ConflictException(MessageCode.BedAlreadyTheirs, bed.BedNumber);
        }

        var now = DateTimeOffset.UtcNow;

        // Carried before the old row is touched, because the new row inherits both.
        var wasOccupied = live.Status == AssignmentStatus.Occupied;
        var occupiedAt = live.OccupiedAt;
        var reservedUntil = live.ReservedUntil;

        live.Status = AssignmentStatus.Released;
        live.ReservedUntil = null;
        live.ReleasedAt = now;
        live.ReleaseReason = ReleaseReason.Corrected;

        // The old row gives up its slot in both indexes here, and only here.
        await _db.SaveChangesAsync(ct);

        await ReleaseLapsedHoldsAsync(bed.Id, now, ct);

        var assignment = new BedAssignmentEntity
        {
            Id = Guid.NewGuid(),
            AdmissionId = admissionId,
            BedId = bed.Id,
            Status = wasOccupied ? AssignmentStatus.Occupied : AssignmentStatus.Reserved,
            ReservedUntil = wasOccupied ? null : reservedUntil,
            OccupiedAt = occupiedAt,

            // A human corrected a human's mistake. Nothing about a correction is the agent's,
            // and recording one as the agent's would poison the report that measures it.
            AssignedBy = AssignedBy.User,
            WorkflowId = null,

            IsDowngrade = live.IsDowngrade,

            ApprovedByStaffMemberId = _currentUser.Id,
            ApprovedAt = now,
            OverrideReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        };

        _db.BedAssignments.Add(assignment);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, BedAssignmentConfiguration.LiveBedUniqueIndex))
        {
            // Somebody else took the replacement bed in the gap. The index is the only thing
            // that can know, and the transaction rolls back - so the patient keeps the wrong
            // bed rather than ending up in none at all, which is the safer of the two.
            throw new ConflictException(MessageCode.BedAlreadyClaimed, bed.BedNumber);
        }

        await transaction.CommitAsync(ct);

        return assignment;
    }

    public async Task<BedOccupancyStatus> GetBedOccupancyAsync(Guid bedId, CancellationToken ct = default)
    {
        // Through the register, so a bed nobody has heard of and a retired one are both the 404
        // the contract publishes. Without it the answer for an unknown id would be a confident
        // "free", which is the one direction this endpoint must never be wrong in.
        _ = await _beds.FindBedAsync(bedId, ct) ?? throw new NotFoundException("Bed", bedId);

        return await _occupancy.GetStatusAsync(bedId, ct);
    }

    /// <summary>
    /// The write, inside a transaction, with the admission row locked and every rule that could
    /// have changed under our feet asked again once the lock is held.
    /// </summary>
    /// <remarks>
    /// Two separate races, needing two different mechanisms.
    ///
    /// **The admission moving.** A duty manager cancelling and a nurse assigning a bed both
    /// read <c>awaiting_bed</c>, both pass the check, and the later write wins - so a cancelled
    /// patient ends up holding a bed. Both rows are legal on their own, so no index can catch
    /// it. <c>SELECT ... FOR UPDATE</c> makes the second request wait, re-read, and get the
    /// honest 409. The same lock <c>AdmissionService</c> takes, for the same reason.
    ///
    /// **The bed being taken.** Two nurses assigning bed 12 in the same instant both pass any
    /// "is it free?" read, however carefully written, because the gap between reading and
    /// writing is where the other INSERT happens. The partial unique index
    /// <c>ux_bed_assignments_live_bed</c> is the guarantee; catching its violation and
    /// answering 409 is the whole of our part. **Do not add a prior read to prevent it** - it
    /// cannot, and it would make the index look like belt-and-braces rather than the rule.
    /// </remarks>
    private async Task<BedAssignmentEntity> WriteAsync(
        Guid admissionId,
        RegisteredBed bed,
        string? overrideReason,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Takes the lock and nothing else. Locking and loading in one composed query puts
        // FOR UPDATE inside a join against patients and bed_assignments, which locks rows
        // nobody asked about.
        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {admissionId} FOR UPDATE", ct);

        // Read committed gives every statement its own snapshot, so this sees whatever the
        // request we queued behind committed, not the row we read before taking the lock.
        var admission = await _db.Admissions
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        // Both halves of the workflow question, the way /arrive and /cancel ask it. Only from
        // awaiting_bed: an admission already holding a bed is refused here rather than quietly
        // given a second one, and ux_bed_assignments_live_admission agrees.
        //
        // Two hops, because the published table has no awaiting_bed -> bed_reserved edge and
        // this endpoint is not the place to invent one. Assigning by hand *is* the proposal and
        // the approval in one act, so both moves happen and both are checked. Nothing observes
        // the middle state - one status is written, at the end.
        // H0 first, before the workflow question. An outpatient is never on the bed board, so
        // the transition check below would refuse this anyway — but it would refuse it as
        // "cannot move from admitted to awaiting_approval", which tells a nurse nothing about
        // why. The real answer is that this patient is here for a scan and needs no bed.
        if (!BedPlacementRules.RequiresBed(admission.Category))
        {
            throw new ConflictException(
                MessageCode.VisitNeedsNoBed, EnumWire.ToWire(admission.Category));
        }

        AdmissionStatusMachine.EnsureMove(
            admission.Status, AdmissionStatus.AwaitingApproval, AdmissionStatus.AwaitingBed);
        AdmissionStatusMachine.EnsureMove(
            AdmissionStatus.AwaitingApproval,
            AdmissionStatus.BedReserved,
            AdmissionStatus.AwaitingApproval);

        // Asked again with the row held rather than trusted from the first read. The category
        // is the input to H2 and to the approval rule, and a clinician can change it in the gap.
        var ward = await FindWardAsync(bed.WardId, ct);

        EnsureMayApprove(admission.Category, ward);

        var isDowngrade = BedPlacementRules.EnsurePlaceable(
            admission.Category,
            admission.Patient.Gender,
            admission.Patient.DateOfBirth,
            admission.IsInfectious,
            ward,
            bed,
            IsDutyManager);

        var now = DateTimeOffset.UtcNow;

        await ReleaseLapsedHoldsAsync(bed.Id, now, ct);

        var assignment = new BedAssignmentEntity
        {
            Id = Guid.NewGuid(),
            AdmissionId = admissionId,
            BedId = bed.Id,
            Status = AssignmentStatus.Reserved,
            ReservedUntil = BedHold.ExpiresAt(admission.ExpectedArrivalAt, now),

            // A human picked this bed, which is what the agent-performance report at step 11
            // measures the agent against.
            AssignedBy = AssignedBy.User,

            // No workflow: nobody asked the agent. Null is the honest value, and it is what
            // tells that report which assignments the agent had no part in.
            WorkflowId = null,

            IsDowngrade = isDowngrade,

            // Assigning by hand is the approval. Stamping who did it here, rather than leaving
            // it to a second endpoint, is what puts the approver in the audit trail at all.
            ApprovedByStaffMemberId = _currentUser.Id,
            ApprovedAt = now,
            OverrideReason = string.IsNullOrWhiteSpace(overrideReason) ? null : overrideReason.Trim()
        };

        admission.Status = AdmissionStatus.BedReserved;
        _db.BedAssignments.Add(assignment);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, BedAssignmentConfiguration.LiveBedUniqueIndex))
        {
            // Somebody else got the bed between the candidate list being drawn and this INSERT.
            // The index is the only thing that can know, and this is the honest answer.
            throw new ConflictException(MessageCode.BedAlreadyClaimed, bed.BedNumber);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, BedAssignmentConfiguration.LiveAdmissionUniqueIndex))
        {
            // The other direction: this admission already holds a bed. Reachable only if a
            // status the workflow forbids and a live assignment row disagree, so it guards
            // against a bug of ours rather than against a user.
            throw new ConflictException(MessageCode.Conflict);
        }

        await transaction.CommitAsync(ct);

        return assignment;
    }

    /// <summary>
    /// Closes any hold on this bed that has run out, so the bed is genuinely free and not only
    /// reported that way.
    /// </summary>
    /// <remarks>
    /// The one place expiry has to be *written* rather than applied at read time.
    /// <c>ux_bed_assignments_live_bed</c> covers every row with status <c>reserved</c> or
    /// <c>occupied</c>, and a unique index cannot consult the clock - so a lapsed hold still
    /// holds its slot in the index even though every read reports the bed free. Leave it and
    /// the bed is free on the candidate list and un-assignable in practice, which is the worst
    /// of both: a nurse picks it and gets a 409 they cannot act on.
    ///
    /// <c>hold_expired</c> is the release reason, which is what that enum member is for. The
    /// row is closed, never deleted - a lapsed hold is part of the audit trail.
    ///
    /// The admission that was holding it goes back to <c>awaiting_bed</c>, which is where
    /// patient-management-plan.md 5.4 puts it: the clock frees a bed and needs nobody's
    /// approval. Leaving it in <c>bed_reserved</c> would show a patient as having a bed that
    /// somebody else is now in.
    ///
    /// It can never be *our* admission. An admission with a lapsed hold is still
    /// <c>bed_reserved</c>, and EnsureMove above has already refused that.
    /// </remarks>
    private async Task ReleaseLapsedHoldsAsync(Guid bedId, DateTimeOffset now, CancellationToken ct)
    {
        var lapsed = await _db.BedAssignments
            .Include(assignment => assignment.Admission)
            .Where(assignment => assignment.BedId == bedId)
            .Where(assignment => assignment.Status == AssignmentStatus.Reserved)
            .Where(assignment => assignment.ReservedUntil != null && assignment.ReservedUntil <= now)
            .ToListAsync(ct);

        foreach (var hold in lapsed)
        {
            hold.Status = AssignmentStatus.Released;
            hold.ReservedUntil = null;
            hold.ReleasedAt = now;
            hold.ReleaseReason = ReleaseReason.HoldExpired;

            if (hold.Admission.Status == AdmissionStatus.BedReserved)
            {
                AdmissionStatusMachine.EnsureMove(
                    hold.Admission.Status,
                    AdmissionStatus.AwaitingBed,
                    AdmissionStatus.BedReserved);

                hold.Admission.Status = AdmissionStatus.AwaitingBed;
            }
        }
    }

    /// <summary>
    /// Refuses a placement only a duty manager may make. A 403 and not a 409: the bed is fine,
    /// the caller is not the person allowed to choose it.
    /// </summary>
    /// <remarks>
    /// Not an <c>[Authorize]</c> policy, because which roles may act depends on the request
    /// body - the ward the chosen bed stands in. The same shape as the check-in rule in
    /// <c>AppointmentService</c> (<c>cl_pat_011</c>), and for the same reason.
    ///
    /// <b>This is the whole of the role split, and it is one question.</b> Does this ward give
    /// the care this patient was assessed as needing? If it does, whoever may place a patient
    /// may place them here - reception and the ward nurse included, intensive care included.
    /// If it does not, in either direction, it is the duty manager's. Which is why
    /// <c>Policies.BedAssigner</c> on the route can be as wide as it is.
    ///
    /// A missing or retired ward falls through untouched. H5 in
    /// <see cref="BedPlacementRules"/> refuses that as a conflict, and answering 403 here would
    /// tell the caller their role was the problem when the ward was.
    /// </remarks>
    private void EnsureMayApprove(AdmissionCategory category, WardEntity? ward)
    {
        // The gate is one question, asked in one place. A ward matching the care level never
        // gets past here - intensive care included, because for an ICU patient that is simply
        // the right bed.
        if (ward is null || IsDutyManager
            || !BedPlacementRules.NeedsDutyManager(category, ward.WardType))
        {
            return;
        }

        // Past the gate it is one of exactly two directions, and the message says which so the
        // caller knows what to look for instead.
        var code = BedPlacementRules.IsDowngrade(category, ward.WardType)
            ? MessageCode.BedDowngradeNeedsDutyManager
            : MessageCode.BedNeedsDutyManager;

        throw new ForbiddenException(
            code, ward.Name, EnumWire.ToWire(ward.WardType), EnumWire.ToWire(category));
    }

    /// <summary>
    /// Whether the person making this request may place a patient anywhere the ward's own
    /// rules allow, rather than only where the care level points.
    /// </summary>
    private bool IsDutyManager => _currentUser.Role == PrincipalRole.DutyManager;

    /// <summary>
    /// The ward a bed stands in, or null when it is missing or retired. The global query filter
    /// hides a retired ward, so null covers both and H5 refuses both the same way.
    /// </summary>
    private Task<WardEntity?> FindWardAsync(Guid wardId, CancellationToken ct)
        => _db.Wards.AsNoTracking().FirstOrDefaultAsync(ward => ward.Id == wardId, ct);

    /// <summary>
    /// The live claim on each of these beds, if any. A hold past its expiry is absent, so the
    /// caller reports that bed free with nobody having done anything.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, LiveClaim>> ClaimsByBedAsync(
        IReadOnlyCollection<Guid> bedIds, DateTimeOffset now, CancellationToken ct)
    {
        if (bedIds.Count == 0)
        {
            return new Dictionary<Guid, LiveClaim>();
        }

        var ids = bedIds.ToList();

        var claims = await _db.BedAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.BedId))
            .Where(BedHold.LiveOn(now))
            .Select(assignment => new LiveClaim(
                assignment.BedId, assignment.AdmissionId, assignment.Status))
            .ToListAsync(ct);

        // ux_bed_assignments_live_bed makes one live claim per bed unique, so these keys cannot
        // collide. ToDictionary rather than a grouping says that out loud.
        return claims.ToDictionary(claim => claim.BedId);
    }

    private static AdmissionBed ToAdmissionBed(
        RegisteredBed bed, string wardName, IReadOnlyDictionary<Guid, LiveClaim> claims)
    {
        var claim = claims.TryGetValue(bed.Id, out var found) ? found : null;

        return new AdmissionBed
        {
            Id = bed.Id,
            WardId = bed.WardId,
            WardName = wardName,
            BedNumber = bed.BedNumber,
            HasIsolation = bed.HasIsolation,
            Condition = bed.Condition,
            Availability = Availability(bed, claim),
            OccupiedByAdmissionId = claim?.AdmissionId,
            CreatedAt = bed.CreatedAt,
            UpdatedAt = bed.UpdatedAt
        };
    }

    /// <summary>
    /// One value out of three facts: our claim, their condition, and the clock. A live claim
    /// wins over <c>out_of_service</c> - see <see cref="BedAvailability"/> for why.
    /// </summary>
    private static BedAvailability Availability(RegisteredBed bed, LiveClaim? claim) => claim switch
    {
        { Status: AssignmentStatus.Occupied } => BedAvailability.Occupied,
        { Status: AssignmentStatus.Reserved } => BedAvailability.Reserved,
        _ => bed.Condition == BedCondition.OutOfService
            ? BedAvailability.OutOfService
            : BedAvailability.Free
    };

    private static bool Matches(BedAvailability availability, BedAvailabilityFilter filter)
        => filter switch
        {
            BedAvailabilityFilter.All => true,
            BedAvailabilityFilter.Free => availability == BedAvailability.Free,
            BedAvailabilityFilter.Reserved => availability == BedAvailability.Reserved,
            BedAvailabilityFilter.Occupied => availability == BedAvailability.Occupied,
            _ => availability == BedAvailability.OutOfService
        };

    private async Task<BedAssignmentResponse> ToResponseAsync(
        BedAssignmentEntity assignment, string wardName, string bedNumber, CancellationToken ct)
    {
        var names = await StaffNames.ByIdAsync(_db, [assignment.ApprovedByStaffMemberId], ct);

        return ToResponse(assignment, wardName, bedNumber, names);
    }

    private static BedAssignmentResponse ToResponse(
        BedAssignmentEntity assignment,
        string wardName,
        string bedNumber,
        IReadOnlyDictionary<Guid, string> names)
        => new()
        {
            Id = assignment.Id,
            AdmissionId = assignment.AdmissionId,
            BedId = assignment.BedId,
            WardName = wardName,
            BedNumber = bedNumber,
            Status = assignment.Status,
            ReservedUntil = assignment.ReservedUntil,
            AssignedBy = assignment.AssignedBy,
            WorkflowId = assignment.WorkflowId,
            IsDowngrade = assignment.IsDowngrade,
            ApprovedByStaffId = assignment.ApprovedByStaffMemberId,
            ApprovedByStaffName = StaffNames.Lookup(names, assignment.ApprovedByStaffMemberId),
            ApprovedAt = assignment.ApprovedAt,
            OverrideReason = assignment.OverrideReason,
            ReleasedAt = assignment.ReleasedAt,
            ReleaseReason = assignment.ReleaseReason,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgres && postgres.ConstraintName == constraintName;

    /// <summary>One live claim on a bed, flattened to the three fields a candidate list reads.</summary>
    private sealed record LiveClaim(Guid BedId, Guid AdmissionId, AssignmentStatus Status);
}
