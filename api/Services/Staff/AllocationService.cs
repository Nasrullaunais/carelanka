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

public sealed class AllocationService : IAllocationService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AllocationService(CareLankaDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AllocationDto>> ListAllocationsAsync(
        ListAllocationsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Allocations.AsNoTracking();

        if (parameters.ShiftId.HasValue)
        {
            query = query.Where(a => a.ShiftId == parameters.ShiftId.Value);
        }

        if (parameters.StaffMemberId.HasValue)
        {
            query = query.Where(a => a.StaffMemberId == parameters.StaffMemberId.Value);
        }

        if (parameters.WardId.HasValue)
        {
            query = query.Where(a => a.Shift.WardId == parameters.WardId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(a => a.Status == parameters.Status.Value);
        }

        if (parameters.From.HasValue)
        {
            query = query.Where(a => a.Shift.Date >= parameters.From.Value);
        }

        if (parameters.To.HasValue)
        {
            query = query.Where(a => a.Shift.Date <= parameters.To.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, parameters.Page);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        var allocations = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var staffIds = allocations.Select(a => a.StaffMemberId).Distinct().ToList();
        var staffNames = await _db.StaffMembers.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var items = allocations.Select(a =>
        {
            var name = staffNames.TryGetValue(a.StaffMemberId, out var n) ? n : string.Empty;
            return MapToDto(a, name);
        }).ToList();

        return PagedResult<AllocationDto>.From(items, page, pageSize, totalItems);
    }

    public async Task<AllocationDto> CreateAllocationAsync(
        CreateAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .Include(s => s.RequiredSkill)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift == null)
        {
            throw new NotFoundException("Shift", request.ShiftId);
        }

        var staffMember = await _db.StaffMembers
            .FirstOrDefaultAsync(s => s.Id == request.StaffMemberId, cancellationToken);

        if (staffMember == null)
        {
            throw new NotFoundException("StaffMember", request.StaffMemberId);
        }

        var (shiftStart, shiftEnd) = GetShiftDateTimeRange(shift);
        var failedChecks = new List<RosterValidationResult>();
        var checkedAt = DateTimeOffset.UtcNow;

        // 1. Staff member active check
        if (!staffMember.IsActive || staffMember.DeletedAt != null)
        {
            failedChecks.Add(new RosterValidationResult
            {
                Check = "staff_active",
                Passed = false,
                Detail = $"Staff member '{staffMember.FullName}' is not active or has been deactivated.",
                CheckedAt = checkedAt
            });
        }

        // 2. Role requirement check
        if (staffMember.Role != shift.RequiredRole)
        {
            failedChecks.Add(new RosterValidationResult
            {
                Check = "role_match",
                Passed = false,
                Detail = $"Staff member role '{staffMember.Role}' does not match required shift role '{shift.RequiredRole}'.",
                CheckedAt = checkedAt
            });
        }

        // 3. Skill requirement currency check
        if (shift.RequiredSkillId.HasValue)
        {
            var hasValidSkill = await _db.StaffMemberSkills.AsNoTracking()
                .AnyAsync(s => s.StaffMemberId == staffMember.Id
                               && s.SkillId == shift.RequiredSkillId.Value
                               && (s.ValidFrom == null || s.ValidFrom.Value <= shift.Date)
                               && (s.ExpiresAt == null || s.ExpiresAt.Value >= shift.Date),
                          cancellationToken);

            if (!hasValidSkill)
            {
                var skillName = shift.RequiredSkill?.Name ?? "Required Skill";
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "required_skill",
                    Passed = false,
                    Detail = $"Staff member does not hold active certification for '{skillName}' on {shift.Date:yyyy-MM-dd}.",
                    CheckedAt = checkedAt
                });
            }
        }

        // 4. Approved leave overlap check
        var leaveEndDateInclusive = DateOnly.FromDateTime(shiftEnd.Date);
        var conflictingLeave = await _db.LeaveRequests.AsNoTracking()
            .FirstOrDefaultAsync(l => l.StaffMemberId == staffMember.Id
                                      && l.Status == LeaveStatus.Approved
                                      && l.StartDate <= leaveEndDateInclusive
                                      && l.EndDate >= shift.Date,
                                 cancellationToken);

        if (conflictingLeave != null)
        {
            failedChecks.Add(new RosterValidationResult
            {
                Check = "leave_conflict",
                Passed = false,
                Detail = $"Staff member has approved leave from {conflictingLeave.StartDate:yyyy-MM-dd} to {conflictingLeave.EndDate:yyyy-MM-dd}.",
                CheckedAt = checkedAt
            });
        }

        // 5. Conflicting confirmed allocation check (with overnight rollover support)
        var nearbyAllocations = await _db.Allocations.AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.StaffMemberId == staffMember.Id
                        && a.Status == AllocationStatus.Confirmed
                        && a.Shift.Date >= shift.Date.AddDays(-2)
                        && a.Shift.Date <= shift.Date.AddDays(2))
            .ToListAsync(cancellationToken);

        Shift? overlappingShift = null;
        foreach (var existing in nearbyAllocations)
        {
            if (existing.ShiftId == shift.Id)
            {
                throw new ConflictException(MessageCode.Conflict, "Staff member already has a confirmed allocation for this shift.");
            }

            var (otherStart, otherEnd) = GetShiftDateTimeRange(existing.Shift);
            if (shiftStart < otherEnd && otherStart < shiftEnd)
            {
                overlappingShift = existing.Shift;
                break;
            }
        }

        if (overlappingShift != null)
        {
            failedChecks.Add(new RosterValidationResult
            {
                Check = "shift_overlap",
                Passed = false,
                Detail = $"Staff member is already allocated to an overlapping shift on {overlappingShift.Date:yyyy-MM-dd} ({overlappingShift.StartTime:HH:mm} - {overlappingShift.EndTime:HH:mm}).",
                CheckedAt = checkedAt
            });
        }

        // If checks failed and override was not specified, reject with AllocationRejectedException
        if (failedChecks.Count > 0)
        {
            if (!request.Override)
            {
                throw new AllocationRejectedException(failedChecks, "Staff allocation was rejected due to staffing rule violations.");
            }
        }

        var allocation = new Allocation
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StaffMemberId = staffMember.Id,
            Status = AllocationStatus.Confirmed,
            Source = AllocationSource.Manual,
            CreatedByStaffId = _currentUser.IsAuthenticated && _currentUser.Id != Guid.Empty ? _currentUser.Id : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Allocations.Add(allocation);
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(allocation, staffMember.FullName);
    }

    public async Task<EndAllocationResponse> EndAllocationAsync(
        Guid id,
        EndAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var allocation = await _db.Allocations
            .Include(a => a.Shift)
                .ThenInclude(s => s.Allocations)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (allocation == null)
        {
            throw new NotFoundException("Allocation", id);
        }

        if (allocation.Status == AllocationStatus.Released)
        {
            throw new ConflictException(MessageCode.Conflict, "Allocation is already ended.");
        }

        if (allocation.Status == AllocationStatus.Cancelled)
        {
            throw new ConflictException(MessageCode.Conflict, "Allocation has been cancelled.");
        }

        allocation.Status = AllocationStatus.Released;
        allocation.EndedReason = request.Reason;
        allocation.EndedAt = DateTimeOffset.UtcNow;
        allocation.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var staffMember = await _db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == allocation.StaffMemberId, cancellationToken);
        var staffName = staffMember?.FullName ?? string.Empty;

        var coverage = ComputeCoverage(allocation.Shift);

        return new EndAllocationResponse
        {
            Allocation = MapToDto(allocation, staffName),
            ShiftCoverage = coverage,
            RosterProposalId = null
        };
    }

    private static (DateTime Start, DateTime End) GetShiftDateTimeRange(Shift shift)
    {
        var start = shift.Date.ToDateTime(shift.StartTime);
        var end = shift.EndTime < shift.StartTime
            ? shift.Date.AddDays(1).ToDateTime(shift.EndTime)
            : shift.Date.ToDateTime(shift.EndTime);
        return (start, end);
    }

    private static ShiftCoverageDto ComputeCoverage(Shift shift)
    {
        var confirmedCount = shift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed);
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
