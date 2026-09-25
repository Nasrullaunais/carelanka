using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public LeaveRequestService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        TimeProvider? clock = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<LeaveRequestDetailDto> CreateLeaveRequestAsync(
        CreateLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var requesterId = _currentUser.Id;
        var requester = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == requesterId && s.IsActive, cancellationToken);

        if (requester == null)
        {
            throw new NotFoundException("StaffMember", requesterId);
        }

        DateOnly startDate;
        DateOnly endDate;

        if (request.Type == LeaveType.ShiftSwap)
        {
            if (!request.SwapShiftId.HasValue || !request.SwapWithStaffMemberId.HasValue)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "Swap shift ID and swap colleague ID are required for a shift swap.");
            }

            if (request.SwapWithStaffMemberId.Value == requesterId)
            {
                throw new ConflictException(MessageCode.Conflict, "Cannot swap shift with yourself.");
            }

            var shift = await _db.Shifts
                .Include(s => s.Allocations)
                .Include(s => s.RequiredSkill)
                .FirstOrDefaultAsync(s => s.Id == request.SwapShiftId.Value, cancellationToken);

            if (shift == null)
            {
                throw new NotFoundException("Shift", request.SwapShiftId.Value);
            }

            var requesterAllocated = shift.Allocations.Any(a =>
                a.StaffMemberId == requesterId && a.Status == AllocationStatus.Confirmed);

            if (!requesterAllocated)
            {
                throw new ConflictException(MessageCode.Conflict, "Staff member is not allocated to the specified shift.");
            }

            var partnerId = request.SwapWithStaffMemberId.Value;
            var partner = await _db.StaffMembers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == partnerId && s.IsActive, cancellationToken);

            if (partner == null)
            {
                throw new NotFoundException("StaffMember", partnerId);
            }

            if (partner.Role != shift.RequiredRole)
            {
                throw new ConflictException(MessageCode.Conflict, "Swap partner does not hold the required role for this shift.");
            }

            if (shift.RequiredSkillId.HasValue)
            {
                var hasSkill = await _db.StaffMemberSkills.AnyAsync(s =>
                    s.StaffMemberId == partnerId
                    && s.SkillId == shift.RequiredSkillId.Value
                    && (!s.ValidFrom.HasValue || s.ValidFrom.Value <= shift.Date)
                    && (!s.ExpiresAt.HasValue || s.ExpiresAt.Value >= shift.Date),
                    cancellationToken);

                if (!hasSkill)
                {
                    throw new ConflictException(MessageCode.Conflict, "Swap partner does not hold the required skill for this shift.");
                }
            }

            var (shiftStart, shiftEnd) = GetShiftDateTimeRange(shift);
            var partnerAllocations = await _db.Allocations
                .Include(a => a.Shift)
                .Where(a => a.StaffMemberId == partnerId
                    && a.Status == AllocationStatus.Confirmed
                    && a.Shift.Date >= shift.Date.AddDays(-1)
                    && a.Shift.Date <= shift.Date.AddDays(1))
                .ToListAsync(cancellationToken);

            foreach (var alloc in partnerAllocations)
            {
                var (allocStart, allocEnd) = GetShiftDateTimeRange(alloc.Shift);
                if (shiftStart < allocEnd && shiftEnd > allocStart)
                {
                    throw new ConflictException(MessageCode.Conflict, "Swap partner is not free during the shift hours.");
                }
            }

            var partnerOnLeave = await _db.LeaveRequests.AnyAsync(l =>
                l.StaffMemberId == partnerId
                && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
                && l.StartDate <= shift.Date
                && l.EndDate >= shift.Date,
                cancellationToken);

            if (partnerOnLeave)
            {
                throw new ConflictException(MessageCode.Conflict, "Swap partner has an existing leave or swap request covering this shift date.");
            }

            var existingSwap = await _db.LeaveRequests.AnyAsync(l =>
                l.StaffMemberId == requesterId
                && l.SwapShiftId == shift.Id
                && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved),
                cancellationToken);

            if (existingSwap)
            {
                throw new ConflictException(MessageCode.Conflict, "A leave or swap request already exists for this shift.");
            }

            startDate = shift.Date;
            endDate = shift.Date;
        }
        else
        {
            if (!request.StartDate.HasValue || !request.EndDate.HasValue)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "Start date and end date are required.");
            }

            startDate = request.StartDate.Value;
            endDate = request.EndDate.Value;

            if (startDate > endDate)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "Start date must not be after end date.");
            }

            var overlappingLeave = await _db.LeaveRequests.AnyAsync(l =>
                l.StaffMemberId == requesterId
                && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
                && l.StartDate <= endDate
                && l.EndDate >= startDate,
                cancellationToken);

            if (overlappingLeave)
            {
                throw new ConflictException(MessageCode.Conflict, "A pending or approved leave request already exists covering this date range.");
            }
        }

        var now = _clock.GetUtcNow();
        var leaveRequest = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            StaffMemberId = requesterId,
            Type = request.Type,
            StartDate = startDate,
            EndDate = endDate,
            Reason = request.Reason,
            Status = LeaveStatus.Pending,
            SwapShiftId = request.SwapShiftId,
            SwapWithStaffMemberId = request.SwapWithStaffMemberId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.LeaveRequests.Add(leaveRequest);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetLeaveRequestDetailAsync(leaveRequest.Id, cancellationToken);
    }

    public async Task<PagedResult<LeaveRequestDto>> ListLeaveRequestsAsync(
        ListLeaveRequestsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != PrincipalRole.HospitalAdministrator &&
            _currentUser.Role != PrincipalRole.DutyManager)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        var query = _db.LeaveRequests.AsNoTracking();

        if (parameters.StaffMemberId.HasValue)
        {
            query = query.Where(l => l.StaffMemberId == parameters.StaffMemberId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(l => l.Status == parameters.Status.Value);
        }

        if (parameters.Type.HasValue)
        {
            query = query.Where(l => l.Type == parameters.Type.Value);
        }

        if (parameters.From.HasValue)
        {
            query = query.Where(l => l.StartDate >= parameters.From.Value);
        }

        if (parameters.To.HasValue)
        {
            query = query.Where(l => l.StartDate <= parameters.To.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(l => l.Status == LeaveStatus.Pending ? 0 : 1)
            .ThenBy(l => l.StartDate)
            .ThenBy(l => l.CreatedAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var staffIds = items.Select(l => l.StaffMemberId).Distinct().ToList();
        var staffMap = await _db.StaffMembers.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var dtos = items.Select(l =>
        {
            var staffName = staffMap.TryGetValue(l.StaffMemberId, out var name) ? name : string.Empty;
            return MapToDto(l, staffName, _clock);
        }).ToList();

        return PagedResult<LeaveRequestDto>.From(dtos, parameters.Page, parameters.PageSize, totalItems);
    }

    public async Task<LeaveRequestDetailDto> GetLeaveRequestDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var leave = await _db.LeaveRequests
            .Include(l => l.SwapShift)
                .ThenInclude(s => s!.Allocations)
            .Include(l => l.SwapShift)
                .ThenInclude(s => s!.RequiredSkill)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (leave == null)
        {
            throw new NotFoundException("LeaveRequest", id);
        }

        if (_currentUser.Role != PrincipalRole.HospitalAdministrator &&
            _currentUser.Role != PrincipalRole.DutyManager &&
            _currentUser.Id != leave.StaffMemberId)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        var staff = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == leave.StaffMemberId, cancellationToken);
        var staffName = staff?.FullName ?? string.Empty;

        var affectedShifts = await ComputeAffectedShiftsAsync(leave, cancellationToken);

        return new LeaveRequestDetailDto
        {
            Id = leave.Id,
            StaffMemberId = leave.StaffMemberId,
            StaffName = staffName,
            Type = leave.Type,
            IsUrgent = ComputeIsUrgent(leave.Type, leave.StartDate, _clock),
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            Reason = leave.Reason,
            Status = leave.Status,
            ReviewedByStaffId = leave.ReviewedByStaffMemberId,
            ReviewedAt = leave.ReviewedByStaffMemberId.HasValue ? leave.UpdatedAt : null,
            ReviewNotes = leave.ReviewNotes,
            SwapWithStaffMemberId = leave.SwapWithStaffMemberId,
            SwapShiftId = leave.SwapShiftId,
            CreatedAt = leave.CreatedAt,
            AffectedShifts = affectedShifts
        };
    }

    public async Task<DecideLeaveResponse> DecideLeaveRequestAsync(
        Guid id,
        DecideLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != PrincipalRole.HospitalAdministrator &&
            _currentUser.Role != PrincipalRole.DutyManager)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        var leave = await _db.LeaveRequests
            .Include(l => l.SwapShift)
                .ThenInclude(s => s!.Allocations)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (leave == null)
        {
            throw new NotFoundException("LeaveRequest", id);
        }

        if (leave.StaffMemberId == _currentUser.Id)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        var isApprove = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(request.Decision, "reject", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isReject)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "Decision must be either 'approve' or 'reject'.");
        }

        if (leave.Status != LeaveStatus.Pending)
        {
            var targetStatus = isApprove ? LeaveStatus.Approved : LeaveStatus.Rejected;
            throw new IllegalTransitionException("LeaveRequest", leave.Status.ToString(), targetStatus.ToString());
        }

        var now = _clock.GetUtcNow();
        leave.ReviewedByStaffMemberId = _currentUser.Id;
        leave.ReviewNotes = request.Notes;
        leave.UpdatedAt = now;

        var releasedSummaries = new List<AllocationSummaryDto>();

        if (isReject)
        {
            leave.Status = LeaveStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            leave.Status = LeaveStatus.Approved;

            if (leave.Type == LeaveType.ShiftSwap && leave.SwapShiftId.HasValue && leave.SwapWithStaffMemberId.HasValue)
            {
                var outgoingAllocation = await _db.Allocations
                    .Include(a => a.Shift)
                    .FirstOrDefaultAsync(a => a.ShiftId == leave.SwapShiftId.Value
                        && a.StaffMemberId == leave.StaffMemberId
                        && a.Status == AllocationStatus.Confirmed, cancellationToken);

                if (outgoingAllocation != null)
                {
                    var incomingAllocation = new Allocation
                    {
                        Id = Guid.NewGuid(),
                        ShiftId = leave.SwapShiftId.Value,
                        StaffMemberId = leave.SwapWithStaffMemberId.Value,
                        Status = AllocationStatus.Confirmed,
                        Source = AllocationSource.SwapRequest,
                        CreatedByStaffId = _currentUser.Id,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _db.Allocations.Add(incomingAllocation);

                    outgoingAllocation.Status = AllocationStatus.Released;
                    outgoingAllocation.EndedReason = AllocationEndReason.SwappedOut;
                    outgoingAllocation.EndedAt = now;
                    outgoingAllocation.UpdatedAt = now;
                    outgoingAllocation.ReplacedByAllocationId = incomingAllocation.Id;

                    var ward = await _db.Wards.AsNoTracking()
                        .FirstOrDefaultAsync(w => w.Id == outgoingAllocation.Shift.WardId, cancellationToken);
                    var staffMember = await _db.StaffMembers.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == outgoingAllocation.StaffMemberId, cancellationToken);

                    releasedSummaries.Add(ToAllocationSummary(outgoingAllocation, ward?.Name ?? string.Empty, staffMember?.FullName ?? string.Empty));
                }
            }
            else
            {
                var leaveStartDateTime = leave.StartDate.ToDateTime(TimeOnly.MinValue);
                var leaveEndDateTime = leave.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

                var allocations = await _db.Allocations
                    .Include(a => a.Shift)
                    .Where(a => a.StaffMemberId == leave.StaffMemberId
                        && a.Status == AllocationStatus.Confirmed
                        && a.Shift.Date >= leave.StartDate.AddDays(-1)
                        && a.Shift.Date <= leave.EndDate)
                    .ToListAsync(cancellationToken);

                var overlappingAllocations = allocations.Where(a =>
                {
                    var (start, end) = GetShiftDateTimeRange(a.Shift);
                    return start < leaveEndDateTime && end > leaveStartDateTime;
                }).ToList();

                var wardIds = overlappingAllocations.Select(a => a.Shift.WardId).Distinct().ToList();
                var wardMap = await _db.Wards.AsNoTracking()
                    .Where(w => wardIds.Contains(w.Id))
                    .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

                var staffMember = await _db.StaffMembers.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == leave.StaffMemberId, cancellationToken);
                var staffName = staffMember?.FullName ?? string.Empty;

                foreach (var allocation in overlappingAllocations)
                {
                    allocation.Status = AllocationStatus.Released;
                    allocation.EndedReason = AllocationEndReason.LeaveApproved;
                    allocation.EndedAt = now;
                    allocation.UpdatedAt = now;

                    var wardName = wardMap.TryGetValue(allocation.Shift.WardId, out var wName) ? wName : string.Empty;
                    releasedSummaries.Add(ToAllocationSummary(allocation, wardName, staffName));
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        var requester = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == leave.StaffMemberId, cancellationToken);

        return new DecideLeaveResponse
        {
            LeaveRequest = MapToDto(leave, requester?.FullName ?? string.Empty, _clock),
            ReleasedAllocations = releasedSummaries,
            RosterProposalIds = Array.Empty<Guid>()
        };
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> GetMyLeaveRequestsAsync(
        LeaveStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var staffId = _currentUser.Id;
        var query = _db.LeaveRequests.AsNoTracking()
            .Where(l => l.StaffMemberId == staffId);

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        var leaves = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        var requester = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);
        var staffName = requester?.FullName ?? string.Empty;

        return leaves.Select(l => MapToDto(l, staffName, _clock)).ToList();
    }

    public async Task WithdrawLeaveRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var leave = await _db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (leave == null)
        {
            throw new NotFoundException("LeaveRequest", id);
        }

        if (leave.StaffMemberId != _currentUser.Id)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        if (leave.Status != LeaveStatus.Pending)
        {
            throw new IllegalTransitionException("LeaveRequest", leave.Status.ToString(), LeaveStatus.Withdrawn.ToString());
        }

        leave.Status = LeaveStatus.Withdrawn;
        leave.UpdatedAt = _clock.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AffectedShiftDto>> ComputeAffectedShiftsAsync(
        LeaveRequest leave,
        CancellationToken cancellationToken)
    {
        var affectedShifts = new List<AffectedShiftDto>();

        if (leave.Type == LeaveType.ShiftSwap)
        {
            if (leave.SwapShiftId.HasValue)
            {
                var shift = leave.SwapShift ?? await _db.Shifts
                    .Include(s => s.Allocations)
                    .Include(s => s.RequiredSkill)
                    .FirstOrDefaultAsync(s => s.Id == leave.SwapShiftId.Value, cancellationToken);

                if (shift != null)
                {
                    var ward = await _db.Wards.AsNoTracking()
                        .FirstOrDefaultAsync(w => w.Id == shift.WardId, cancellationToken);
                    var wardName = ward?.Name ?? string.Empty;
                    var shiftSummary = MapToShiftSummaryDto(shift, wardName);
                    var coverageIfApproved = ComputeCoverage(shift, 0);

                    affectedShifts.Add(new AffectedShiftDto
                    {
                        Shift = shiftSummary,
                        CoverageIfApproved = coverageIfApproved
                    });
                }
            }

            return affectedShifts;
        }

        var leaveStartDateTime = leave.StartDate.ToDateTime(TimeOnly.MinValue);
        var leaveEndDateTime = leave.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var allocations = await _db.Allocations
            .Include(a => a.Shift)
                .ThenInclude(s => s.Allocations)
            .Include(a => a.Shift)
                .ThenInclude(s => s.RequiredSkill)
            .Where(a => a.StaffMemberId == leave.StaffMemberId
                && a.Status == AllocationStatus.Confirmed
                && a.Shift.Date >= leave.StartDate.AddDays(-1)
                && a.Shift.Date <= leave.EndDate)
            .ToListAsync(cancellationToken);

        var overlappingAllocations = allocations.Where(a =>
        {
            var (start, end) = GetShiftDateTimeRange(a.Shift);
            return start < leaveEndDateTime && end > leaveStartDateTime;
        }).ToList();

        var wardIds = overlappingAllocations.Select(a => a.Shift.WardId).Distinct().ToList();
        var wardMap = await _db.Wards.AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var grouped = overlappingAllocations
            .GroupBy(a => a.ShiftId)
            .Select(g => new { Shift = g.First().Shift, Count = g.Count() })
            .ToList();

        foreach (var item in grouped)
        {
            var wardName = wardMap.TryGetValue(item.Shift.WardId, out var name) ? name : string.Empty;
            var shiftSummary = MapToShiftSummaryDto(item.Shift, wardName);
            var coverageIfApproved = ComputeCoverage(item.Shift, -item.Count);

            affectedShifts.Add(new AffectedShiftDto
            {
                Shift = shiftSummary,
                CoverageIfApproved = coverageIfApproved
            });
        }

        return affectedShifts;
    }

    private static (DateTime Start, DateTime End) GetShiftDateTimeRange(Shift shift)
    {
        var start = shift.Date.ToDateTime(shift.StartTime);
        var end = shift.EndTime < shift.StartTime
            ? shift.Date.AddDays(1).ToDateTime(shift.EndTime)
            : shift.Date.ToDateTime(shift.EndTime);
        return (start, end);
    }

    private static ShiftCoverageDto ComputeCoverage(Shift shift, int offset)
    {
        var confirmedCount = Math.Max(0, shift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed) + offset);
        var shortfall = Math.Max(0, shift.MinimumHeadcount - confirmedCount);

        CoverageStatus status;
        if (confirmedCount >= shift.HeadcountNeeded)
        {
            status = CoverageStatus.Adequate;
        }
        else if (confirmedCount >= shift.MinimumHeadcount)
        {
            status = CoverageStatus.AtMinimum;
        }
        else if (confirmedCount > 0)
        {
            status = CoverageStatus.Understaffed;
        }
        else
        {
            status = CoverageStatus.Critical;
        }

        return new ShiftCoverageDto
        {
            ConfirmedCount = confirmedCount,
            HeadcountNeeded = shift.HeadcountNeeded,
            MinimumHeadcount = shift.MinimumHeadcount,
            Status = status,
            ShortfallToMinimum = shortfall
        };
    }

    private static ShiftSummaryDto MapToShiftSummaryDto(Shift shift, string wardName)
    {
        var crossesMidnight = shift.EndTime < shift.StartTime;

        return new ShiftSummaryDto
        {
            Id = shift.Id,
            WardId = shift.WardId,
            WardName = wardName,
            Date = shift.Date,
            StartTime = shift.StartTime.ToString("HH:mm"),
            EndTime = shift.EndTime.ToString("HH:mm"),
            CrossesMidnight = crossesMidnight,
            RequiredRole = shift.RequiredRole,
            RequiredSkillId = shift.RequiredSkillId,
            RequiredSkillName = shift.RequiredSkill?.Name,
            HeadcountNeeded = shift.HeadcountNeeded,
            MinimumHeadcount = shift.MinimumHeadcount,
            CreatedAt = shift.CreatedAt,
            UpdatedAt = shift.UpdatedAt,
            Coverage = ComputeCoverage(shift, 0)
        };
    }

    private static bool ComputeIsUrgent(LeaveType type, DateOnly startDate, TimeProvider? clock = null)
    {
        if (type is LeaveType.Sick or LeaveType.Emergency)
        {
            return true;
        }

        var now = clock?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        var within24Hours = DateOnly.FromDateTime(now.AddHours(24).DateTime);
        return startDate <= within24Hours;
    }

    private static LeaveRequestDto MapToDto(LeaveRequest leave, string staffName, TimeProvider? clock = null) => new()
    {
        Id = leave.Id,
        StaffMemberId = leave.StaffMemberId,
        StaffName = staffName,
        Type = leave.Type,
        IsUrgent = ComputeIsUrgent(leave.Type, leave.StartDate, clock),
        StartDate = leave.StartDate,
        EndDate = leave.EndDate,
        Reason = leave.Reason,
        Status = leave.Status,
        ReviewedByStaffId = leave.ReviewedByStaffMemberId,
        ReviewedAt = leave.ReviewedByStaffMemberId.HasValue ? leave.UpdatedAt : null,
        ReviewNotes = leave.ReviewNotes,
        SwapWithStaffMemberId = leave.SwapWithStaffMemberId,
        SwapShiftId = leave.SwapShiftId,
        CreatedAt = leave.CreatedAt
    };

    private static AllocationSummaryDto ToAllocationSummary(Allocation a, string wardName, string staffName) => new()
    {
        AllocationId = a.Id,
        ShiftId = a.ShiftId,
        WardName = wardName,
        Date = a.Shift.Date,
        StartTime = a.Shift.StartTime.ToString("HH:mm"),
        EndTime = a.Shift.EndTime.ToString("HH:mm"),
        StaffMemberId = a.StaffMemberId,
        StaffName = staffName,
        Status = a.Status
    };
}
