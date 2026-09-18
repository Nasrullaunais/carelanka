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
        var now = DateTimeOffset.UtcNow;

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

            .OrderBy(bed => bed.WardName)
            .ThenBy(bed => bed.BedNumber)
            .ToList();

        return PagedResult<AdmissionBed>.From(
            candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page,
            pageSize,
            candidates.Count);
    }

    public async Task<BedAssignmentResponse> AssignManuallyAsync(
        Guid admissionId, AssignBedRequest request, CancellationToken ct = default)
    {
        var bedId = request.BedId!.Value;

        var bed = await _beds.FindBedAsync(bedId, ct)

            ?? throw new NotFoundException("Bed", bedId);

        var ward = await FindWardAsync(bed.WardId, ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

        EnsureMayApprove(admission.Category, ward);

        BedPlacementRules.EnsurePlaceable(
            admission.Category,
            admission.Patient.Gender,
            admission.Patient.DateOfBirth,
            admission.IsInfectious,
            ward,
            bed,
            IsDutyManager);

        var workflowId = await ResolveWorkflowAsync(admissionId, bed.Id, request.WorkflowId, ct);

        var assignment = await WriteAsync(
            admissionId, bed, request.OverrideReason, workflowId, ct);

        return await ToResponseAsync(assignment, ward!.Name, bed.BedNumber, ct);
    }

    /// <summary>
    /// Keeps the workflow id only if that run really did offer this bed for this visit.
    /// </summary>
    /// <remarks>
    /// An id that matches nothing is dropped and the assignment is recorded as a manual one,
    /// which is what <c>patient-spec.yaml</c> publishes. Refusing instead was the other option and
    /// is worse: a stale id from a screen somebody left open would stop a nurse bedding a patient
    /// at all, where recording it as manual costs an attribution and is true - that bed was not
    /// suggested by any run.
    /// </remarks>
    private async Task<Guid?> ResolveWorkflowAsync(
        Guid admissionId, Guid bedId, Guid? workflowId, CancellationToken ct)
    {
        if (workflowId is not { } id)
        {
            return null;
        }

        var suggested = await _db.BedSuggestions
            .AsNoTracking()
            .Where(suggestion => suggestion.WorkflowId == id)
            .Where(suggestion => suggestion.AdmissionId == admissionId)
            .SelectMany(suggestion => suggestion.Candidates)
            .AnyAsync(candidate => candidate.BedId == bedId, ct);

        return suggested ? id : null;
    }

    public async Task<BedAssignmentResponse> CorrectBedAsync(
        Guid admissionId, CorrectBedRequest request, CancellationToken ct = default)
    {
        var bedId = request.BedId!.Value;

        var bed = await _beds.FindBedAsync(bedId, ct)
            ?? throw new NotFoundException("Bed", bedId);

        var ward = await FindWardAsync(bed.WardId, ct);

        var admission = await _db.Admissions
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

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

            ?? throw new ConflictException(MessageCode.BedNotAssigned);

        if (live.BedId == bed.Id)
        {
            throw new ConflictException(MessageCode.BedAlreadyTheirs, bed.BedNumber);
        }

        var now = DateTimeOffset.UtcNow;

        var wasOccupied = live.Status == AssignmentStatus.Occupied;
        var occupiedAt = live.OccupiedAt;
        var reservedUntil = live.ReservedUntil;

        live.Status = AssignmentStatus.Released;
        live.ReservedUntil = null;
        live.ReleasedAt = now;
        live.ReleaseReason = ReleaseReason.Corrected;

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
            throw new ConflictException(MessageCode.BedAlreadyClaimed, bed.BedNumber);
        }

        await transaction.CommitAsync(ct);

        return assignment;
    }

    public async Task<BedOccupancyStatus> GetBedOccupancyAsync(Guid bedId, CancellationToken ct = default)
    {
        _ = await _beds.FindBedAsync(bedId, ct) ?? throw new NotFoundException("Bed", bedId);

        return await _occupancy.GetStatusAsync(bedId, ct);
    }

    private async Task<BedAssignmentEntity> WriteAsync(
        Guid admissionId,
        RegisteredBed bed,
        string? overrideReason,
        Guid? workflowId,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {admissionId} FOR UPDATE", ct);

        var admission = await _db.Admissions
            .Include(candidate => candidate.Patient)
            .FirstOrDefaultAsync(candidate => candidate.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

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

            // The agent suggested it; the person pressing the button still approved it, and is
            // still the one stamped below. assigned_by records where the idea came from.
            AssignedBy = workflowId is null ? AssignedBy.User : AssignedBy.Agent,

            WorkflowId = workflowId,

            IsDowngrade = isDowngrade,

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
            throw new ConflictException(MessageCode.BedAlreadyClaimed, bed.BedNumber);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, BedAssignmentConfiguration.LiveAdmissionUniqueIndex))
        {
            throw new ConflictException(MessageCode.Conflict);
        }

        await transaction.CommitAsync(ct);

        return assignment;
    }

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

    private void EnsureMayApprove(AdmissionCategory category, WardEntity? ward)
    {
        if (ward is null || IsDutyManager
            || !BedPlacementRules.NeedsDutyManager(category, ward.WardType))
        {
            return;
        }

        var code = BedPlacementRules.IsDowngrade(category, ward.WardType)
            ? MessageCode.BedDowngradeNeedsDutyManager
            : MessageCode.BedNeedsDutyManager;

        throw new ForbiddenException(
            code, ward.Name, EnumWire.ToWire(ward.WardType), EnumWire.ToWire(category));
    }

    private bool IsDutyManager => _currentUser.Role == PrincipalRole.DutyManager;

    private Task<WardEntity?> FindWardAsync(Guid wardId, CancellationToken ct)
        => _db.Wards.AsNoTracking().FirstOrDefaultAsync(ward => ward.Id == wardId, ct);

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

    private sealed record LiveClaim(Guid BedId, Guid AdmissionId, AssignmentStatus Status);
}
