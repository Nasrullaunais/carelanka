using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class SkillService : ISkillService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SkillService(CareLankaDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SkillDto>> ListSkillsAsync(string? search, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.Skills.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SkillDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                StaffCount = _db.StaffMemberSkills
                    .Count(sms => sms.SkillId == s.Id &&
                                  _db.StaffMembers.Any(sm => sm.Id == sms.StaffMemberId) &&
                                  (sms.ValidFrom == null || sms.ValidFrom <= today) &&
                                  (sms.ExpiresAt == null || sms.ExpiresAt >= today))
            })
            .ToListAsync(ct);
    }

    public async Task<SkillDto> CreateSkillAsync(CreateSkillRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        var exists = await _db.Skills.AnyAsync(s => s.Name.ToLower() == name.ToLower(), ct);
        if (exists)
        {
            throw new ConflictException(MessageCode.Conflict);
        }

        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        _db.Skills.Add(skill);
        await _db.SaveChangesAsync(ct);

        return new SkillDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Description = skill.Description,
            StaffCount = 0
        };
    }

    public async Task<SkillDto> UpdateSkillAsync(Guid id, UpdateSkillRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        var skill = await _db.Skills
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (skill is null)
        {
            throw new NotFoundException("Skill", id);
        }

        var duplicate = await _db.Skills.AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower(), ct);
        if (duplicate)
        {
            throw new ConflictException(MessageCode.Conflict);
        }

        skill.Name = name;
        skill.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var staffCount = await _db.StaffMemberSkills
            .CountAsync(sms => sms.SkillId == skill.Id &&
                               _db.StaffMembers.Any(sm => sm.Id == sms.StaffMemberId) &&
                               (sms.ValidFrom == null || sms.ValidFrom <= today) &&
                               (sms.ExpiresAt == null || sms.ExpiresAt >= today), ct);

        return new SkillDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Description = skill.Description,
            StaffCount = staffCount
        };
    }

    public async Task RetireSkillAsync(Guid id, CancellationToken ct = default)
    {
        var skill = await _db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null)
        {
            throw new NotFoundException("Skill", id);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shiftRequiresSkill = await _db.Shifts
            .AnyAsync(s => s.RequiredSkillId == id && s.Date >= today, ct);

        if (shiftRequiresSkill)
        {
            throw new ConflictException(MessageCode.Conflict);
        }

        skill.IsActive = false;
        skill.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StaffSkillDto>> ListStaffSkillsAsync(Guid staffMemberId, CancellationToken ct = default)
    {
        if (_currentUser.Role != PrincipalRole.HospitalAdministrator &&
            _currentUser.Role != PrincipalRole.DutyManager &&
            _currentUser.Id != staffMemberId)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        var staffExists = await _db.StaffMembers.AnyAsync(s => s.Id == staffMemberId, ct);
        if (!staffExists)
        {
            throw new NotFoundException("StaffMember", staffMemberId);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var staffSkills = await _db.StaffMemberSkills
            .AsNoTracking()
            .Where(sms => sms.StaffMemberId == staffMemberId)
            .Include(sms => sms.Skill)
            .OrderBy(sms => sms.Skill.Name)
            .ToListAsync(ct);

        return staffSkills.Select(sms => new StaffSkillDto
        {
            SkillId = sms.SkillId,
            SkillName = sms.Skill.Name,
            ValidFrom = sms.ValidFrom,
            ExpiresAt = sms.ExpiresAt,
            IsValid = (!sms.ValidFrom.HasValue || sms.ValidFrom.Value <= today) &&
                      (!sms.ExpiresAt.HasValue || sms.ExpiresAt.Value >= today),
            GrantedAt = sms.CreatedAt
        }).ToList();
    }

    public async Task<StaffSkillDto> GrantStaffSkillAsync(Guid staffMemberId, GrantStaffSkillRequest request, CancellationToken ct = default)
    {
        var staffMember = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == staffMemberId, ct);
        if (staffMember is null)
        {
            throw new NotFoundException("StaffMember", staffMemberId);
        }

        var skill = await _db.Skills.FirstOrDefaultAsync(s => s.Id == request.SkillId, ct);
        if (skill is null)
        {
            throw new NotFoundException("Skill", request.SkillId);
        }

        if (request.ValidFrom.HasValue && request.ExpiresAt.HasValue && request.ValidFrom.Value > request.ExpiresAt.Value)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        var alreadyHeld = await _db.StaffMemberSkills
            .AnyAsync(sms => sms.StaffMemberId == staffMemberId && sms.SkillId == request.SkillId, ct);

        if (alreadyHeld)
        {
            throw new ConflictException(MessageCode.Conflict);
        }

        var staffSkill = new StaffMemberSkill
        {
            Id = Guid.NewGuid(),
            StaffMemberId = staffMemberId,
            SkillId = request.SkillId,
            ValidFrom = request.ValidFrom,
            ExpiresAt = request.ExpiresAt
        };

        _db.StaffMemberSkills.Add(staffSkill);
        await _db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new StaffSkillDto
        {
            SkillId = skill.Id,
            SkillName = skill.Name,
            ValidFrom = staffSkill.ValidFrom,
            ExpiresAt = staffSkill.ExpiresAt,
            IsValid = (!staffSkill.ValidFrom.HasValue || staffSkill.ValidFrom.Value <= today) &&
                      (!staffSkill.ExpiresAt.HasValue || staffSkill.ExpiresAt.Value >= today),
            GrantedAt = staffSkill.CreatedAt
        };
    }

    public async Task<RevokeStaffSkillResponse> RevokeStaffSkillAsync(Guid staffMemberId, Guid skillId, CancellationToken ct = default)
    {
        var staffMember = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == staffMemberId, ct);
        if (staffMember is null)
        {
            throw new NotFoundException("StaffMember", staffMemberId);
        }

        var staffSkill = await _db.StaffMemberSkills
            .FirstOrDefaultAsync(sms => sms.StaffMemberId == staffMemberId && sms.SkillId == skillId, ct);

        if (staffSkill is null)
        {
            throw new NotFoundException("StaffMemberSkill", skillId);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var allocations = await _db.Allocations
            .AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.StaffMemberId == staffMemberId &&
                        a.Shift.RequiredSkillId == skillId &&
                        a.Shift.Date >= today &&
                        a.Status == AllocationStatus.Confirmed)
            .ToListAsync(ct);

        var wardIds = allocations.Select(a => a.Shift.WardId).Distinct().ToList();
        var wardMap = await _db.Wards
            .AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, ct);

        var affectedAllocations = allocations.Select(a => new AllocationSummaryDto
        {
            AllocationId = a.Id,
            ShiftId = a.ShiftId,
            WardName = wardMap.TryGetValue(a.Shift.WardId, out var wardName) ? wardName : string.Empty,
            Date = a.Shift.Date,
            StartTime = a.Shift.StartTime.ToString("HH:mm"),
            EndTime = a.Shift.EndTime.ToString("HH:mm"),
            StaffMemberId = a.StaffMemberId,
            StaffName = staffMember.FullName,
            Status = a.Status
        }).ToList();

        _db.StaffMemberSkills.Remove(staffSkill);
        await _db.SaveChangesAsync(ct);

        return new RevokeStaffSkillResponse
        {
            AffectedAllocations = affectedAllocations
        };
    }
}
