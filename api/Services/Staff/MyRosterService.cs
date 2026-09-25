using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CareLanka.Api.Services.Staff;

public sealed class MyRosterService : IMyRosterService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;
    private readonly int _clockInWindowMinutesBefore;

    public MyRosterService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        TimeProvider? clock = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock ?? TimeProvider.System;
        _clockInWindowMinutesBefore = configuration?.GetValue<int?>("Staff:ClockInWindowMinutesBefore") ?? 30;
    }

    public async Task<IReadOnlyList<MyShiftDto>> GetMyShiftsAsync(
        GetMyShiftsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var staffId = _currentUser.Id;
        var now = _clock.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var today = DateOnly.FromDateTime(nowUtc);

        var query = _db.Allocations
            .AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.StaffMemberId == staffId);

        if (parameters.From.HasValue)
        {
            query = query.Where(a => a.Shift.Date >= parameters.From.Value);
        }
        else if (!parameters.IncludePast)
        {
            query = query.Where(a => a.Shift.Date >= today.AddDays(-1));
        }

        if (parameters.To.HasValue)
        {
            query = query.Where(a => a.Shift.Date <= parameters.To.Value);
        }

        var candidateAllocations = await query
            .OrderBy(a => a.Shift.Date)
            .ThenBy(a => a.Shift.StartTime)
            .ToListAsync(cancellationToken);

        var allocationIds = candidateAllocations.Select(a => a.Id).ToList();

        var reassignedAllocationIds = await _db.Allocations
            .AsNoTracking()
            .Where(a => a.ReplacedByAllocationId.HasValue && allocationIds.Contains(a.ReplacedByAllocationId.Value))
            .Select(a => a.ReplacedByAllocationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var reassignedSet = reassignedAllocationIds.ToHashSet();

        var wardIds = candidateAllocations.Select(a => a.Shift.WardId).Distinct().ToList();
        var wardMap = await _db.Wards
            .AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var result = new List<MyShiftDto>();

        foreach (var allocation in candidateAllocations)
        {
            var (shiftStartUtc, shiftEndUtc) = GetShiftDateTimeRangeUtc(allocation.Shift);

            if (!parameters.IncludePast && shiftEndUtc < nowUtc)
            {
                continue;
            }

            var windowStartUtc = shiftStartUtc.AddMinutes(-_clockInWindowMinutesBefore);
            var windowEndUtc = shiftEndUtc;
            var canClockIn = allocation.Status == AllocationStatus.Confirmed &&
                             allocation.ClockedInAt == null &&
                             nowUtc >= windowStartUtc &&
                             nowUtc <= windowEndUtc;

            wardMap.TryGetValue(allocation.Shift.WardId, out var wardName);

            result.Add(new MyShiftDto
            {
                AllocationId = allocation.Id,
                ShiftId = allocation.ShiftId,
                WardName = wardName ?? string.Empty,
                Date = allocation.Shift.Date,
                StartTime = allocation.Shift.StartTime.ToString("HH:mm"),
                EndTime = allocation.Shift.EndTime.ToString("HH:mm"),
                CrossesMidnight = allocation.Shift.EndTime < allocation.Shift.StartTime,
                Status = allocation.Status,
                ClockedInAt = allocation.ClockedInAt,
                ClockedOutAt = allocation.ClockedOutAt,
                CanClockIn = canClockIn,
                WasReassigned = reassignedSet.Contains(allocation.Id)
            });
        }

        return result;
    }

    public async Task<AllocationDto> ClockInAsync(
        Guid allocationId,
        CancellationToken cancellationToken = default)
    {
        var allocation = await _db.Allocations
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);

        if (allocation == null)
        {
            throw new NotFoundException("Allocation", allocationId);
        }

        if (allocation.StaffMemberId != _currentUser.Id)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        if (allocation.Status != AllocationStatus.Confirmed)
        {
            throw new ConflictException(MessageCode.Conflict, "Allocation is not confirmed.");
        }

        if (allocation.ClockedInAt != null)
        {
            throw new ConflictException(MessageCode.Conflict, "Already clocked in.");
        }

        var (shiftStartUtc, shiftEndUtc) = GetShiftDateTimeRangeUtc(allocation.Shift);
        var windowStartUtc = shiftStartUtc.AddMinutes(-_clockInWindowMinutesBefore);
        var windowEndUtc = shiftEndUtc;
        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        if (nowUtc < windowStartUtc || nowUtc > windowEndUtc)
        {
            throw new ConflictException(MessageCode.Conflict, "Outside the permitted clock-in window.");
        }

        var now = _clock.GetUtcNow();
        allocation.ClockedInAt = now;
        allocation.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        var staffMember = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == allocation.StaffMemberId, cancellationToken);
        var staffName = staffMember?.FullName ?? string.Empty;

        return MapToDto(allocation, staffName);
    }

    public async Task<AllocationDto> ClockOutAsync(
        Guid allocationId,
        CancellationToken cancellationToken = default)
    {
        var allocation = await _db.Allocations
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);

        if (allocation == null)
        {
            throw new NotFoundException("Allocation", allocationId);
        }

        if (allocation.StaffMemberId != _currentUser.Id)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        if (allocation.Status != AllocationStatus.Confirmed)
        {
            throw new ConflictException(MessageCode.Conflict, "Allocation is not confirmed.");
        }

        if (allocation.ClockedInAt == null)
        {
            throw new ConflictException(MessageCode.Conflict, "Not clocked in.");
        }

        if (allocation.ClockedOutAt != null)
        {
            throw new ConflictException(MessageCode.Conflict, "Already clocked out.");
        }

        var now = _clock.GetUtcNow();
        allocation.ClockedOutAt = now;
        allocation.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        var staffMember = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == allocation.StaffMemberId, cancellationToken);
        var staffName = staffMember?.FullName ?? string.Empty;

        return MapToDto(allocation, staffName);
    }

    private static (DateTime Start, DateTime End) GetShiftDateTimeRangeUtc(Shift shift)
    {
        var start = shift.Date.ToDateTime(shift.StartTime, DateTimeKind.Utc);
        var end = shift.EndTime < shift.StartTime
            ? shift.Date.AddDays(1).ToDateTime(shift.EndTime, DateTimeKind.Utc)
            : shift.Date.ToDateTime(shift.EndTime, DateTimeKind.Utc);
        return (start, end);
    }

    private static AllocationDto MapToDto(Allocation allocation, string staffName) => new()
    {
        Id = allocation.Id,
        ShiftId = allocation.ShiftId,
        StaffMemberId = allocation.StaffMemberId,
        StaffName = staffName,
        Status = allocation.Status,
        Source = allocation.Source,
        EndedAt = allocation.EndedAt,
        EndedReason = allocation.EndedReason,
        ReplacedByAllocationId = allocation.ReplacedByAllocationId,
        ClockedInAt = allocation.ClockedInAt,
        ClockedOutAt = allocation.ClockedOutAt,
        CreatedByStaffId = allocation.CreatedByStaffId,
        RosterProposalId = allocation.RosterProposalId,
        CreatedAt = allocation.CreatedAt
    };
}
