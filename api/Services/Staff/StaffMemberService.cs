using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class StaffMemberService : IStaffMemberService
{
    private readonly CareLankaDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ICurrentUser _currentUser;

    public StaffMemberService(
        CareLankaDbContext db,
        IPasswordService passwords,
        ICurrentUser currentUser)
    {
        _db = db;
        _passwords = passwords;
        _currentUser = currentUser;
    }

    public async Task<StaffMemberDto> CreateStaffMemberAsync(CreateStaffMemberRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var activeConflict = await _db.StaffMembers
            .AnyAsync(s => s.Email.ToLower() == email && s.IsActive, ct);
        if (activeConflict)
        {
            throw new StaffEmailConflictException(null, $"An active staff member already uses the email '{email}'.");
        }

        var inactiveStaff = await _db.StaffMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Email.ToLower() == email && !s.IsActive, ct);
        if (inactiveStaff != null)
        {
            throw new StaffEmailConflictException(
                inactiveStaff.Id,
                $"An inactive staff member already exists with email '{email}'. Use POST /api/staff/{inactiveStaff.Id}/reactivate to reactivate.");
        }

        var skillIds = request.SkillIds?.Distinct().ToList() ?? new List<Guid>();
        if (skillIds.Count > 0)
        {
            var validSkillIds = await _db.Skills
                .Where(s => skillIds.Contains(s.Id) && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            var invalidIds = skillIds.Except(validSkillIds).ToList();
            if (invalidIds.Count > 0)
            {
                throw new InvalidSkillsBadRequestException(
                    invalidIds,
                    $"The following skill IDs do not exist: {string.Join(", ", invalidIds)}");
            }
        }

        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var staff = new StaffMember
        {
            Id = id,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = _passwords.Hash(request.TemporaryPassword),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = request.Role,
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.StaffMembers.Add(staff);

        if (skillIds.Count > 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var skillId in skillIds)
            {
                _db.StaffMemberSkills.Add(new StaffMemberSkill
                {
                    Id = Guid.NewGuid(),
                    StaffMemberId = id,
                    SkillId = skillId,
                    ValidFrom = today,
                    ExpiresAt = null,
                    CreatedAt = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(staff);
    }

    public async Task<PagedResult<StaffSummaryDto>> ListStaffAsync(ListStaffQueryParameters parameters, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = parameters.IncludeInactive
            ? _db.StaffMembers.IgnoreQueryFilters()
            : _db.StaffMembers.Where(s => s.IsActive);

        if (parameters.Role.HasValue)
        {
            query = query.Where(s => s.Role == parameters.Role.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Department))
        {
            var dept = parameters.Department.Trim().ToLower();
            query = query.Where(s => s.Department != null && s.Department.ToLower() == dept);
        }

        if (parameters.Skill.HasValue)
        {
            var skillId = parameters.Skill.Value;
            query = query.Where(s => _db.StaffMemberSkills.Any(sms =>
                sms.StaffMemberId == s.Id &&
                sms.SkillId == skillId &&
                (sms.ValidFrom == null || sms.ValidFrom <= today) &&
                (sms.ExpiresAt == null || sms.ExpiresAt >= today)));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim().ToLowerInvariant();
            var empTerm = term.StartsWith("emp-", StringComparison.OrdinalIgnoreCase) ? term[4..] : term;
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(term) ||
                s.LastName.ToLower().Contains(term) ||
                (s.FirstName + " " + s.LastName).ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term) ||
                s.Id.ToString().ToLower().Contains(empTerm));
        }

        var totalItems = await query.CountAsync(ct);

        var sortBy = parameters.SortBy?.ToLowerInvariant() ?? "full_name";
        var isDesc = string.Equals(parameters.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

        query = sortBy switch
        {
            "role" => isDesc ? query.OrderByDescending(s => s.Role) : query.OrderBy(s => s.Role),
            "department" => isDesc ? query.OrderByDescending(s => s.Department) : query.OrderBy(s => s.Department),
            "created_at" => isDesc ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
            _ => isDesc
                ? query.OrderByDescending(s => s.FirstName).ThenByDescending(s => s.LastName)
                : query.OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
        };

        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize < 1 ? 20 : (parameters.PageSize > 100 ? 100 : parameters.PageSize);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var staffIds = items.Select(s => s.Id).ToList();
        var skillCounts = await _db.StaffMemberSkills
            .Where(sms => staffIds.Contains(sms.StaffMemberId) &&
                          _db.Skills.Any(sk => sk.Id == sms.SkillId && sk.IsActive) &&
                          (sms.ValidFrom == null || sms.ValidFrom <= today) &&
                          (sms.ExpiresAt == null || sms.ExpiresAt >= today))
            .GroupBy(sms => sms.StaffMemberId)
            .Select(g => new { StaffMemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.StaffMemberId, g => g.Count, ct);

        var dtos = items.Select(s => ToSummaryDto(s, skillCounts.GetValueOrDefault(s.Id, 0))).ToList();
        return PagedResult<StaffSummaryDto>.From(dtos, page, pageSize, totalItems);
    }

    public async Task<StaffMemberDetailDto> GetStaffMemberAsync(Guid id, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (staff is null)
        {
            throw new NotFoundException("StaffMember", id);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var skills = await _db.StaffMemberSkills
            .AsNoTracking()
            .Include(sms => sms.Skill)
            .Where(sms => sms.StaffMemberId == id &&
                          sms.Skill.IsActive &&
                          (sms.ValidFrom == null || sms.ValidFrom <= today) &&
                          (sms.ExpiresAt == null || sms.ExpiresAt >= today))
            .OrderBy(sms => sms.Skill.Name)
            .Select(sms => new StaffSkillDto
            {
                SkillId = sms.SkillId,
                SkillName = sms.Skill.Name,
                ValidFrom = sms.ValidFrom,
                ExpiresAt = sms.ExpiresAt,
                IsValid = true,
                GrantedAt = sms.CreatedAt
            })
            .ToListAsync(ct);

        var allocations = await _db.Allocations
            .AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.StaffMemberId == id &&
                        a.Status == AllocationStatus.Confirmed &&
                        a.Shift.Date >= today)
            .OrderBy(a => a.Shift.Date)
            .ThenBy(a => a.Shift.StartTime)
            .ToListAsync(ct);

        var wardIds = allocations.Select(a => a.Shift.WardId).Distinct().ToList();
        var wardMap = await _db.Wards
            .AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);

        var allocationSummaries = allocations.Select(a =>
            ToAllocationSummary(a, wardMap.GetValueOrDefault(a.Shift.WardId, string.Empty), staff.FullName))
            .ToList();

        return ToDetailDto(staff, skills, allocationSummaries);
    }

    public async Task<UpdateStaffMemberResponse> UpdateStaffMemberAsync(Guid id, UpdateStaffMemberRequest request, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (staff is null)
        {
            throw new NotFoundException("StaffMember", id);
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName)) staff.FirstName = request.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(request.LastName)) staff.LastName = request.LastName.Trim();
        if (request.PhoneNumber is not null) staff.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (request.Department is not null) staff.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();

        var affectedAllocations = new List<AllocationSummaryDto>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.Role.HasValue && request.Role.Value != staff.Role)
        {
            var newRole = request.Role.Value;
            var allocations = await _db.Allocations
                .AsNoTracking()
                .Include(a => a.Shift)
                .Where(a => a.StaffMemberId == id &&
                            a.Status == AllocationStatus.Confirmed &&
                            a.Shift.Date >= today &&
                            a.Shift.RequiredRole != newRole)
                .OrderBy(a => a.Shift.Date)
                .ThenBy(a => a.Shift.StartTime)
                .ToListAsync(ct);

            if (allocations.Count > 0)
            {
                var wardIds = allocations.Select(a => a.Shift.WardId).Distinct().ToList();
                var wardMap = await _db.Wards
                    .AsNoTracking()
                    .Where(w => wardIds.Contains(w.Id))
                    .ToDictionaryAsync(w => w.Id, w => w.Name, ct);

                affectedAllocations = allocations.Select(a =>
                    ToAllocationSummary(a, wardMap.GetValueOrDefault(a.Shift.WardId, string.Empty), staff.FullName))
                    .ToList();
            }

            staff.Role = newRole;
        }

        staff.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new UpdateStaffMemberResponse
        {
            StaffMember = ToDto(staff),
            AffectedAllocations = affectedAllocations
        };
    }

    public async Task DeactivateStaffMemberAsync(Guid id, DeactivateStaffMemberRequest request, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (staff is null)
        {
            throw new NotFoundException("StaffMember", id);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcomingAllocations = await _db.Allocations
            .AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.StaffMemberId == id &&
                        a.Status == AllocationStatus.Confirmed &&
                        a.Shift.Date >= today)
            .OrderBy(a => a.Shift.Date)
            .ThenBy(a => a.Shift.StartTime)
            .ToListAsync(ct);

        if (upcomingAllocations.Count > 0)
        {
            var wardIds = upcomingAllocations.Select(a => a.Shift.WardId).Distinct().ToList();
            var wardMap = await _db.Wards
                .AsNoTracking()
                .Where(w => wardIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.Name, ct);

            var affected = upcomingAllocations.Select(a =>
                ToAllocationSummary(a, wardMap.GetValueOrDefault(a.Shift.WardId, string.Empty), staff.FullName))
                .ToList();

            throw new StaffDeactivationConflictException(
                affected,
                "Cannot deactivate staff member who still holds confirmed future allocations.");
        }

        var now = DateTimeOffset.UtcNow;
        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.StaffMemberId == id && rt.RevokedAt == null && rt.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = "staff_deactivated";
        }

        staff.IsActive = false;
        staff.DeletedAt = now;
        staff.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StaffMemberDto> ReactivateStaffMemberAsync(Guid id, CancellationToken ct = default)
    {
        var staff = await _db.StaffMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (staff is null)
        {
            throw new NotFoundException("StaffMember", id);
        }

        if (staff.IsActive)
        {
            throw new ConflictException(MessageCode.Conflict, "Staff member is already active.");
        }

        var emailInUse = await _db.StaffMembers
            .AnyAsync(s => s.Id != id && s.Email.ToLower() == staff.Email.ToLower() && s.IsActive, ct);

        if (emailInUse)
        {
            throw new ConflictException(MessageCode.Conflict, "The email is now in use by an active staff member.");
        }

        staff.IsActive = true;
        staff.DeletedAt = null;
        staff.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDto(staff);
    }

    public static string ToEmployeeNumber(Guid id) => $"EMP-{id.ToString("N")[..8].ToUpperInvariant()}";

    public static StaffMemberDto ToDto(StaffMember member) => new()
    {
        Id = member.Id,
        EmployeeNumber = ToEmployeeNumber(member.Id),
        FirstName = member.FirstName,
        LastName = member.LastName,
        FullName = member.FullName,
        Email = member.Email,
        PhoneNumber = member.PhoneNumber,
        Role = member.Role,
        Department = member.Department,
        IsActive = member.IsActive,
        CreatedAt = member.CreatedAt,
        UpdatedAt = member.UpdatedAt
    };

    public static StaffSummaryDto ToSummaryDto(StaffMember member, int skillCount) => new()
    {
        Id = member.Id,
        FullName = member.FullName,
        Role = member.Role,
        Department = member.Department,
        IsActive = member.IsActive,
        SkillCount = skillCount
    };

    public static StaffMemberDetailDto ToDetailDto(
        StaffMember member,
        IReadOnlyList<StaffSkillDto> skills,
        IReadOnlyList<AllocationSummaryDto> upcomingAllocations) => new()
    {
        Id = member.Id,
        EmployeeNumber = ToEmployeeNumber(member.Id),
        FirstName = member.FirstName,
        LastName = member.LastName,
        FullName = member.FullName,
        Email = member.Email,
        PhoneNumber = member.PhoneNumber,
        Role = member.Role,
        Department = member.Department,
        IsActive = member.IsActive,
        CreatedAt = member.CreatedAt,
        UpdatedAt = member.UpdatedAt,
        Skills = skills,
        UpcomingAllocations = upcomingAllocations,
        LeaveBalanceDays = null
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
