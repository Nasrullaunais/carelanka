using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Staff;

/// <summary>
/// Allow-listed, read-only tools for evaluating staffing options.
/// Strictly read-only queries with AsNoTracking. Zero database writes.
/// </summary>
public sealed class StaffAllocationAgentTools : IStaffAllocationAgentTools
{
    private readonly CareLankaDbContext _db;

    public StaffAllocationAgentTools(CareLankaDbContext db)
    {
        _db = db;
    }

    public async Task<UnderstaffedShiftInfo?> GetUnderstaffedShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts.AsNoTracking()
            .Include(s => s.RequiredSkill)
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

        if (shift is null)
        {
            return null;
        }

        var wardName = await _db.Wards.AsNoTracking()
            .Where(w => w.Id == shift.WardId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Ward";

        var confirmedCount = shift.Allocations
            .Count(a => a.Status == AllocationStatus.Confirmed && a.EndedAt == null);

        var gap = Math.Max(0, shift.MinimumHeadcount - confirmedCount);

        return new UnderstaffedShiftInfo(
            shift,
            wardName,
            confirmedCount,
            shift.MinimumHeadcount,
            shift.HeadcountNeeded,
            gap);
    }

    public async Task<IReadOnlyList<StaffCandidate>> FindEligibleStaffAsync(
        Guid shiftId,
        IReadOnlyList<Guid>? excludeStaffIds = null,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

        if (shift is null)
        {
            return [];
        }

        var query = _db.StaffMembers.AsNoTracking()
            .Where(s => s.IsActive && s.DeletedAt == null && s.Role == shift.RequiredRole);

        if (excludeStaffIds is { Count: > 0 })
        {
            query = query.Where(s => !excludeStaffIds.Contains(s.Id));
        }

        if (shift.RequiredSkillId.HasValue)
        {
            var requiredSkillId = shift.RequiredSkillId.Value;
            var shiftDate = shift.Date;

            var qualifiedStaffIds = await _db.StaffMemberSkills.AsNoTracking()
                .Where(sk => sk.SkillId == requiredSkillId
                          && (sk.ValidFrom == null || sk.ValidFrom.Value <= shiftDate)
                          && (sk.ExpiresAt == null || sk.ExpiresAt.Value >= shiftDate))
                .Select(sk => sk.StaffMemberId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(s => qualifiedStaffIds.Contains(s.Id));
        }

        var staffList = await query
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(cancellationToken);

        return staffList.Select(s => new StaffCandidate(
            s.Id,
            s.FullName,
            s.Role,
            s.Department,
            s.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<StaffCandidate>> FindFreeStaffAsync(
        IReadOnlyCollection<StaffCandidate> candidates,
        Shift shift,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var (shiftStart, shiftEnd) = GetShiftDateTimeRange(shift);
        var leaveEndDateInclusive = DateOnly.FromDateTime(shiftEnd.Date);
        var candidateIds = candidates.Select(c => c.Id).Distinct().ToList();

        var onLeaveList = await _db.LeaveRequests.AsNoTracking()
            .Where(l => candidateIds.Contains(l.StaffMemberId)
                     && l.Status == LeaveStatus.Approved
                     && l.StartDate <= leaveEndDateInclusive
                     && l.EndDate >= shift.Date)
            .Select(l => l.StaffMemberId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var onLeaveStaffIds = onLeaveList.ToHashSet();

        var nearbyAllocations = await _db.Allocations.AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => candidateIds.Contains(a.StaffMemberId)
                     && a.Status == AllocationStatus.Confirmed
                     && a.EndedAt == null
                     && a.Shift.Date >= shift.Date.AddDays(-2)
                     && a.Shift.Date <= shift.Date.AddDays(2))
            .ToListAsync(cancellationToken);

        var allocatedStaffIds = new HashSet<Guid>();
        foreach (var alloc in nearbyAllocations)
        {
            var (otherStart, otherEnd) = GetShiftDateTimeRange(alloc.Shift);
            if (shiftStart < otherEnd && otherStart < shiftEnd)
            {
                allocatedStaffIds.Add(alloc.StaffMemberId);
            }
        }

        return candidates
            .Where(c => !onLeaveStaffIds.Contains(c.Id) && !allocatedStaffIds.Contains(c.Id))
            .ToList();
    }

    public async Task<IReadOnlyList<CascadingSwapCandidate>> FindCascadingSwapCandidatesAsync(
        Shift targetShift,
        IReadOnlyList<Guid>? excludeStaffIds = null,
        IReadOnlyList<Guid>? excludeWardIds = null,
        CancellationToken cancellationToken = default)
    {
        var eligibleStaff = await FindEligibleStaffAsync(targetShift.Id, excludeStaffIds, cancellationToken);
        if (eligibleStaff.Count == 0)
        {
            return [];
        }

        var (targetStart, targetEnd) = GetShiftDateTimeRange(targetShift);
        var leaveEndDateInclusive = DateOnly.FromDateTime(targetEnd.Date);
        var eligibleIds = eligibleStaff.Select(e => e.Id).ToList();

        var onLeaveList = await _db.LeaveRequests.AsNoTracking()
            .Where(l => eligibleIds.Contains(l.StaffMemberId)
                     && l.Status == LeaveStatus.Approved
                     && l.StartDate <= leaveEndDateInclusive
                     && l.EndDate >= targetShift.Date)
            .Select(l => l.StaffMemberId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var onLeaveStaffIds = onLeaveList.ToHashSet();

        var availableEligible = eligibleStaff
            .Where(e => !onLeaveStaffIds.Contains(e.Id))
            .ToDictionary(e => e.Id);

        if (availableEligible.Count == 0)
        {
            return [];
        }

        var otherShiftsQuery = _db.Shifts.AsNoTracking()
            .Include(s => s.Allocations)
            .Where(s => s.WardId != targetShift.WardId
                     && s.Date >= targetShift.Date.AddDays(-1)
                     && s.Date <= targetShift.Date.AddDays(1));

        if (excludeWardIds is { Count: > 0 })
        {
            otherShiftsQuery = otherShiftsQuery.Where(s => !excludeWardIds.Contains(s.WardId));
        }

        var otherShifts = await otherShiftsQuery.ToListAsync(cancellationToken);

        var donorWardIds = otherShifts.Select(s => s.WardId).Distinct().ToList();
        var wardMap = await _db.Wards.AsNoTracking()
            .Where(w => donorWardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var swapCandidates = new List<CascadingSwapCandidate>();

        foreach (var donorShift in otherShifts)
        {
            var (donorStart, donorEnd) = GetShiftDateTimeRange(donorShift);
            if (!(targetStart < donorEnd && donorStart < targetEnd))
            {
                continue;
            }

            var confirmedAllocations = donorShift.Allocations
                .Where(a => a.Status == AllocationStatus.Confirmed && a.EndedAt == null)
                .ToList();

            var confirmedCount = confirmedAllocations.Count;
            var minHeadcount = donorShift.MinimumHeadcount;

            // Hard constraint: donor ward must strictly exceed minimum headcount,
            // so removing 1 staff member keeps the ward at or above minimum.
            if (confirmedCount <= minHeadcount)
            {
                continue;
            }

            var surplus = confirmedCount - minHeadcount;
            var wardName = wardMap.TryGetValue(donorShift.WardId, out var wn) ? wn : "Ward";

            foreach (var alloc in confirmedAllocations)
            {
                if (availableEligible.TryGetValue(alloc.StaffMemberId, out var staffCandidate))
                {
                    swapCandidates.Add(new CascadingSwapCandidate(
                        staffCandidate,
                        alloc,
                        donorShift,
                        wardName,
                        confirmedCount,
                        minHeadcount,
                        surplus));
                }
            }
        }

        return swapCandidates
            .OrderByDescending(c => c.SurplusCount)
            .ThenBy(c => c.Staff.FullName)
            .ToList();
    }

    public static (DateTime Start, DateTime End) GetShiftDateTimeRange(Shift shift)
    {
        var start = shift.Date.ToDateTime(shift.StartTime, DateTimeKind.Utc);
        var end = shift.EndTime < shift.StartTime
            ? shift.Date.AddDays(1).ToDateTime(shift.EndTime, DateTimeKind.Utc)
            : shift.Date.ToDateTime(shift.EndTime, DateTimeKind.Utc);

        return (start, end);
    }
}
