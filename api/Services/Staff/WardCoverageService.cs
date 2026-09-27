using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class WardCoverageService : IWardCoverageService
{
    private readonly CareLankaDbContext _db;
    private readonly TimeProvider _clock;

    public WardCoverageService(CareLankaDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<WardCoverageOverviewResponse> GetWardCoverageAsync(
        DateTimeOffset? at,
        CancellationToken cancellationToken = default)
    {
        var pointInTime = at ?? _clock.GetUtcNow();
        var pointInTimeUtc = pointInTime.UtcDateTime;
        var targetDate = DateOnly.FromDateTime(pointInTimeUtc);
        var prevDate = targetDate.AddDays(-1);

        var wards = await _db.Wards.AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

        var wardIds = wards.Select(w => w.Id).ToList();

        var candidateShifts = await _db.Shifts
            .AsNoTracking()
            .Include(s => s.Allocations)
            .Where(s => wardIds.Contains(s.WardId) && (s.Date == targetDate || s.Date == prevDate))
            .ToListAsync(cancellationToken);

        var wardRules = await _db.WardStaffingRules.AsNoTracking()
            .Where(r => wardIds.Contains(r.WardId))
            .ToListAsync(cancellationToken);

        var activeShifts = candidateShifts.Where(s => IsShiftActiveAt(s, pointInTimeUtc)).ToList();
        var confirmedAllocations = activeShifts
            .SelectMany(s => s.Allocations)
            .Where(a => a.Status == AllocationStatus.Confirmed)
            .ToList();

        var staffMemberIds = confirmedAllocations.Select(a => a.StaffMemberId).Distinct().ToList();
        var staffMemberRoles = await _db.StaffMembers.AsNoTracking()
            .Where(s => staffMemberIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Role, cancellationToken);

        var wardCoverageList = new List<WardCoverageDto>(wards.Count);

        foreach (var ward in wards)
        {
            var shiftsForWard = activeShifts.Where(s => s.WardId == ward.Id).ToList();

            Guid? currentShiftId = null;
            int onDutyCount = 0;
            int minimumHeadcount = 0;
            int headcountNeeded = 0;
            var byRole = new Dictionary<string, int>();

            if (shiftsForWard.Count > 0)
            {
                currentShiftId = shiftsForWard[0].Id;
                minimumHeadcount = shiftsForWard.Sum(s => s.MinimumHeadcount);
                headcountNeeded = shiftsForWard.Sum(s => s.HeadcountNeeded);

                foreach (var shift in shiftsForWard)
                {
                    foreach (var alloc in shift.Allocations.Where(a => a.Status == AllocationStatus.Confirmed))
                    {
                        onDutyCount++;
                        var role = staffMemberRoles.TryGetValue(alloc.StaffMemberId, out var r) ? r : shift.RequiredRole;
                        var roleKey = EnumWire.ToWire(role);
                        byRole[roleKey] = byRole.GetValueOrDefault(roleKey, 0) + 1;
                    }
                }
            }
            else
            {
                var rules = wardRules.Where(r => r.WardId == ward.Id).ToList();
                minimumHeadcount = rules.Sum(r => r.MinimumHeadcount);
                headcountNeeded = minimumHeadcount;
            }

            CoverageStatus status;
            if (onDutyCount >= headcountNeeded && headcountNeeded > 0)
            {
                status = CoverageStatus.Adequate;
            }
            else if (onDutyCount >= minimumHeadcount && minimumHeadcount > 0)
            {
                status = CoverageStatus.AtMinimum;
            }
            else if (onDutyCount > 0)
            {
                status = CoverageStatus.Understaffed;
            }
            else
            {
                status = minimumHeadcount == 0 && headcountNeeded == 0 ? CoverageStatus.Adequate : CoverageStatus.Critical;
            }

            wardCoverageList.Add(new WardCoverageDto
            {
                WardId = ward.Id,
                WardName = ward.Name,
                CurrentShiftId = currentShiftId,
                OnDutyCount = onDutyCount,
                MinimumHeadcount = minimumHeadcount,
                HeadcountNeeded = headcountNeeded,
                Status = status,
                ByRole = byRole
            });
        }

        return new WardCoverageOverviewResponse
        {
            GeneratedAt = pointInTime,
            Wards = wardCoverageList
        };
    }

    private static bool IsShiftActiveAt(Shift shift, DateTime pointInTimeUtc)
    {
        var startUtc = shift.Date.ToDateTime(shift.StartTime, DateTimeKind.Utc);
        var endUtc = shift.EndTime < shift.StartTime
            ? shift.Date.AddDays(1).ToDateTime(shift.EndTime, DateTimeKind.Utc)
            : shift.Date.ToDateTime(shift.EndTime, DateTimeKind.Utc);

        return pointInTimeUtc >= startUtc && pointInTimeUtc < endUtc;
    }
}
