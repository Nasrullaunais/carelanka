using System.Globalization;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class ShiftService : IShiftService
{
    private readonly CareLankaDbContext _db;

    private static readonly Dictionary<string, DayOfWeek> WeekdayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mon"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday
    };

    public ShiftService(CareLankaDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ShiftSummaryDto>> ListShiftsAsync(
        ListShiftsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.To < parameters.From)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
        }

        var query = _db.Shifts
            .AsNoTracking()
            .Include(s => s.RequiredSkill)
            .Include(s => s.Allocations)
            .Where(s => s.Date >= parameters.From && s.Date <= parameters.To);

        if (parameters.WardId.HasValue)
        {
            query = query.Where(s => s.WardId == parameters.WardId.Value);
        }

        if (parameters.Role.HasValue)
        {
            query = query.Where(s => s.RequiredRole == parameters.Role.Value);
        }

        var shifts = await query.ToListAsync(cancellationToken);

        var wardIds = shifts.Select(s => s.WardId).Distinct().ToList();
        var wardMap = await _db.Wards.AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var summaries = shifts.Select(s =>
        {
            var wardName = wardMap.TryGetValue(s.WardId, out var name) ? name : "Unknown";
            return MapToSummaryDto(s, wardName);
        }).ToList();

        if (parameters.CoverageStatus.HasValue)
        {
            summaries = summaries.Where(s => s.Coverage.Status == parameters.CoverageStatus.Value).ToList();
        }

        var isDesc = string.Equals(parameters.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var sortBy = parameters.SortBy?.ToLowerInvariant() ?? "date";

        summaries = sortBy switch
        {
            "ward" => isDesc
                ? summaries.OrderByDescending(s => s.WardName).ThenByDescending(s => s.Date).ThenByDescending(s => s.StartTime).ToList()
                : summaries.OrderBy(s => s.WardName).ThenBy(s => s.Date).ThenBy(s => s.StartTime).ToList(),
            "coverage_status" => isDesc
                ? summaries.OrderByDescending(s => s.Coverage.Status).ThenByDescending(s => s.Date).ThenByDescending(s => s.StartTime).ToList()
                : summaries.OrderBy(s => s.Coverage.Status).ThenBy(s => s.Date).ThenBy(s => s.StartTime).ToList(),
            _ => isDesc
                ? summaries.OrderByDescending(s => s.Date).ThenByDescending(s => s.StartTime).ToList()
                : summaries.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList()
        };

        var page = Math.Max(1, parameters.Page);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 100);
        var totalItems = summaries.Count;
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

        var pagedItems = summaries.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<ShiftSummaryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Items = pagedItems
        };
    }

    public async Task<ShiftSummaryDto> CreateShiftAsync(
        CreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var ward = await _db.Wards.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WardId && w.IsActive, cancellationToken);
        if (ward == null)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, $"Ward '{request.WardId}' not found or inactive.");
        }

        Skill? skill = null;
        if (request.RequiredSkillId.HasValue)
        {
            skill = await _db.Skills.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.RequiredSkillId.Value && s.IsActive && s.DeletedAt == null, cancellationToken);
            if (skill == null)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, $"Skill '{request.RequiredSkillId.Value}' not found or inactive.");
            }
        }

        var startTime = ParseTime(request.StartTime, nameof(request.StartTime));
        var endTime = ParseTime(request.EndTime, nameof(request.EndTime));

        if (startTime == endTime)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "Start time and end time cannot be identical.");
        }

        var minimumHeadcount = request.MinimumHeadcount.HasValue && request.MinimumHeadcount.Value > 0
            ? request.MinimumHeadcount.Value
            : await DeriveMinimumHeadcountAsync(request.WardId, request.RequiredRole, request.RequiredSkillId, request.HeadcountNeeded, cancellationToken);

        if (minimumHeadcount > request.HeadcountNeeded)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "Minimum headcount must not exceed headcount needed.");
        }

        var shift = new Shift
        {
            WardId = request.WardId,
            Date = request.Date,
            StartTime = startTime,
            EndTime = endTime,
            RequiredRole = request.RequiredRole,
            RequiredSkillId = request.RequiredSkillId,
            HeadcountNeeded = request.HeadcountNeeded,
            MinimumHeadcount = minimumHeadcount
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);

        shift.RequiredSkill = skill;
        return MapToSummaryDto(shift, ward.Name);
    }

    public async Task<BulkShiftResponse> CreateShiftsBulkAsync(
        BulkShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To < request.From)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
        }

        if (request.Patterns == null || request.Patterns.Count == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "At least one shift pattern is required.");
        }

        var ward = await _db.Wards.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WardId && w.IsActive, cancellationToken);
        if (ward == null)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, $"Ward '{request.WardId}' not found or inactive.");
        }

        var skillIds = request.Patterns
            .Where(p => p.RequiredSkillId.HasValue)
            .Select(p => p.RequiredSkillId!.Value)
            .Distinct()
            .ToList();

        var skillsMap = new Dictionary<Guid, Skill>();
        if (skillIds.Count > 0)
        {
            var skills = await _db.Skills.AsNoTracking()
                .Where(s => skillIds.Contains(s.Id) && s.IsActive && s.DeletedAt == null)
                .ToListAsync(cancellationToken);

            if (skills.Count != skillIds.Count)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "One or more required skills in the patterns were not found or are inactive.");
            }

            skillsMap = skills.ToDictionary(s => s.Id);
        }

        HashSet<DayOfWeek> targetWeekdays;
        if (request.Weekdays != null && request.Weekdays.Count > 0)
        {
            targetWeekdays = new HashSet<DayOfWeek>();
            foreach (var weekdayStr in request.Weekdays)
            {
                if (!WeekdayMap.TryGetValue(weekdayStr, out var dayOfWeek))
                {
                    throw new BadRequestException(MessageCode.ValidationFailed, $"Invalid weekday abbreviation '{weekdayStr}'.");
                }
                targetWeekdays.Add(dayOfWeek);
            }
        }
        else
        {
            targetWeekdays = new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>());
        }

        var parsedPatterns = request.Patterns.Select(p =>
        {
            var start = ParseTime(p.StartTime, nameof(p.StartTime));
            var end = ParseTime(p.EndTime, nameof(p.EndTime));
            if (start == end)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "Start time and end time cannot be identical.");
            }
            return new
            {
                Pattern = p,
                StartTime = start,
                EndTime = end
            };
        }).ToList();

        var existingShifts = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.WardId == request.WardId && s.Date >= request.From && s.Date <= request.To)
            .Select(s => new { s.Date, s.StartTime, s.EndTime, s.RequiredRole })
            .ToListAsync(cancellationToken);

        var existingKeys = new HashSet<(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, StaffRole RequiredRole)>(
            existingShifts.Select(s => (s.Date, s.StartTime, s.EndTime, s.RequiredRole)));

        var wardRules = await _db.WardStaffingRules.AsNoTracking()
            .Where(r => r.WardId == request.WardId)
            .ToListAsync(cancellationToken);

        var createdShifts = new List<Shift>();
        var skippedCount = 0;

        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            if (!targetWeekdays.Contains(date.DayOfWeek))
            {
                continue;
            }

            foreach (var p in parsedPatterns)
            {
                var key = (date, p.StartTime, p.EndTime, p.Pattern.RequiredRole);
                if (existingKeys.Contains(key))
                {
                    skippedCount++;
                    continue;
                }

                int minHeadcount;
                if (p.Pattern.MinimumHeadcount.HasValue && p.Pattern.MinimumHeadcount.Value > 0)
                {
                    minHeadcount = p.Pattern.MinimumHeadcount.Value;
                }
                else
                {
                    WardStaffingRule? matchingRule = null;
                    if (p.Pattern.RequiredSkillId.HasValue)
                    {
                        matchingRule = wardRules.FirstOrDefault(r =>
                            r.RequiredRole == p.Pattern.RequiredRole &&
                            r.RequiredSkillId == p.Pattern.RequiredSkillId.Value);
                    }

                    matchingRule ??= wardRules.FirstOrDefault(r =>
                        r.RequiredRole == p.Pattern.RequiredRole &&
                        r.RequiredSkillId == null);

                    minHeadcount = matchingRule != null ? matchingRule.MinimumHeadcount : p.Pattern.HeadcountNeeded;
                    minHeadcount = Math.Clamp(minHeadcount, 1, p.Pattern.HeadcountNeeded);
                }

                if (minHeadcount > p.Pattern.HeadcountNeeded)
                {
                    minHeadcount = p.Pattern.HeadcountNeeded;
                }

                var shift = new Shift
                {
                    WardId = request.WardId,
                    Date = date,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    RequiredRole = p.Pattern.RequiredRole,
                    RequiredSkillId = p.Pattern.RequiredSkillId,
                    HeadcountNeeded = p.Pattern.HeadcountNeeded,
                    MinimumHeadcount = minHeadcount
                };

                createdShifts.Add(shift);
                existingKeys.Add(key);
            }
        }

        if (createdShifts.Count > 0)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                _db.Shifts.AddRange(createdShifts);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }

        var shiftSummaries = createdShifts.Select(s =>
        {
            if (s.RequiredSkillId.HasValue && skillsMap.TryGetValue(s.RequiredSkillId.Value, out var sk))
            {
                s.RequiredSkill = sk;
            }
            return MapToSummaryDto(s, ward.Name);
        }).ToList();

        return new BulkShiftResponse
        {
            Created = createdShifts.Count,
            Skipped = skippedCount,
            Shifts = shiftSummaries
        };
    }

    public async Task<ShiftDetailDto> GetShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .AsNoTracking()
            .Include(s => s.RequiredSkill)
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (shift == null)
        {
            throw new NotFoundException("Shift", id);
        }

        var ward = await _db.Wards.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == shift.WardId, cancellationToken);
        var wardName = ward?.Name ?? "Unknown";

        var staffMemberIds = shift.Allocations.Select(a => a.StaffMemberId).Distinct().ToList();
        var staffMap = new Dictionary<Guid, string>();
        if (staffMemberIds.Count > 0)
        {
            staffMap = await _db.StaffMembers.IgnoreQueryFilters().AsNoTracking()
                .Where(sm => staffMemberIds.Contains(sm.Id))
                .ToDictionaryAsync(sm => sm.Id, sm => $"{sm.FirstName} {sm.LastName}".Trim(), cancellationToken);
        }

        var allocationDtos = shift.Allocations.Select(a => new AllocationDto
        {
            Id = a.Id,
            ShiftId = a.ShiftId,
            StaffMemberId = a.StaffMemberId,
            StaffName = staffMap.TryGetValue(a.StaffMemberId, out var name) ? name : "Unknown Staff",
            Status = a.Status,
            Source = a.Source,
            EndedAt = a.EndedAt,
            EndedReason = a.EndedReason,
            ReplacedByAllocationId = a.ReplacedByAllocationId,
            ClockedInAt = a.ClockedInAt,
            ClockedOutAt = a.ClockedOutAt,
            CreatedByStaffId = a.CreatedByStaffId,
            RosterProposalId = a.RosterProposalId,
            CreatedAt = a.CreatedAt
        }).ToList();

        var openProposalId = shift.Allocations
            .Where(a => a.RosterProposalId.HasValue)
            .Select(a => a.RosterProposalId)
            .FirstOrDefault();

        var crossesMidnight = shift.EndTime < shift.StartTime;

        return new ShiftDetailDto
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
            Coverage = ComputeCoverage(shift),
            Allocations = allocationDtos,
            OpenProposalId = openProposalId
        };
    }

    public async Task<ShiftSummaryDto> UpdateShiftAsync(
        Guid id,
        CreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .Include(s => s.RequiredSkill)
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (shift == null)
        {
            throw new NotFoundException("Shift", id);
        }

        var ward = await _db.Wards.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WardId && w.IsActive, cancellationToken);
        if (ward == null)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, $"Ward '{request.WardId}' not found or inactive.");
        }

        Skill? skill = null;
        if (request.RequiredSkillId.HasValue)
        {
            skill = await _db.Skills.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.RequiredSkillId.Value && s.IsActive && s.DeletedAt == null, cancellationToken);
            if (skill == null)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, $"Skill '{request.RequiredSkillId.Value}' not found or inactive.");
            }
        }

        var startTime = ParseTime(request.StartTime, nameof(request.StartTime));
        var endTime = ParseTime(request.EndTime, nameof(request.EndTime));

        if (startTime == endTime)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "Start time and end time cannot be identical.");
        }

        var minimumHeadcount = request.MinimumHeadcount.HasValue && request.MinimumHeadcount.Value > 0
            ? request.MinimumHeadcount.Value
            : await DeriveMinimumHeadcountAsync(request.WardId, request.RequiredRole, request.RequiredSkillId, request.HeadcountNeeded, cancellationToken);

        if (minimumHeadcount > request.HeadcountNeeded)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "Minimum headcount must not exceed headcount needed.");
        }

        shift.WardId = request.WardId;
        shift.Date = request.Date;
        shift.StartTime = startTime;
        shift.EndTime = endTime;
        shift.RequiredRole = request.RequiredRole;
        shift.RequiredSkillId = request.RequiredSkillId;
        shift.HeadcountNeeded = request.HeadcountNeeded;
        shift.MinimumHeadcount = minimumHeadcount;
        shift.RequiredSkill = skill;

        await _db.SaveChangesAsync(cancellationToken);

        return MapToSummaryDto(shift, ward.Name);
    }

    public async Task CancelShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (shift == null)
        {
            throw new NotFoundException("Shift", id);
        }

        var startDateTime = shift.Date.ToDateTime(shift.StartTime, DateTimeKind.Utc);
        if (startDateTime <= DateTime.UtcNow)
        {
            throw new ConflictException(MessageCode.Conflict, "The shift has already started.");
        }

        var confirmedAllocations = shift.Allocations
            .Where(a => a.Status == AllocationStatus.Confirmed)
            .ToList();

        foreach (var allocation in confirmedAllocations)
        {
            allocation.Status = AllocationStatus.Released;
            allocation.EndedReason = AllocationEndReason.ShiftCancelled;
            allocation.EndedAt = DateTimeOffset.UtcNow;
        }

        // Physical deletion vs FK Restrict:
        // When no allocations exist on the shift, the row can be physically removed cleanly.
        // When allocations exist on the shift, physical deletion is prevented by the Restrict FK
        // (fk_allocations_shifts_shift_id) to preserve audit trails.
        if (!shift.Allocations.Any())
        {
            _db.Shifts.Remove(shift);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WardStaffingRuleDto>> GetWardStaffingRulesAsync(
        Guid wardId,
        CancellationToken cancellationToken = default)
    {
        var ward = await _db.Wards.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == wardId, cancellationToken);
        if (ward == null)
        {
            throw new NotFoundException("Ward", wardId);
        }

        var rules = await _db.WardStaffingRules
            .AsNoTracking()
            .Include(r => r.RequiredSkill)
            .Where(r => r.WardId == wardId)
            .OrderBy(r => r.RequiredRole)
            .ToListAsync(cancellationToken);

        return rules.Select(r => new WardStaffingRuleDto
        {
            Id = r.Id,
            WardId = r.WardId,
            RequiredRole = r.RequiredRole,
            RequiredSkillId = r.RequiredSkillId,
            RequiredSkillName = r.RequiredSkill?.Name,
            MinimumHeadcount = r.MinimumHeadcount
        }).ToList();
    }

    public async Task<ReplaceWardStaffingRulesResponse> ReplaceWardStaffingRulesAsync(
        Guid wardId,
        IReadOnlyList<WardStaffingRuleInput> rules,
        CancellationToken cancellationToken = default)
    {
        var ward = await _db.Wards.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == wardId, cancellationToken);
        if (ward == null)
        {
            throw new NotFoundException("Ward", wardId);
        }

        var duplicate = rules
            .GroupBy(r => new { r.RequiredRole, r.RequiredSkillId })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate != null)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "Duplicate staffing rule for the same role and skill combination.");
        }

        var skillIds = rules
            .Where(r => r.RequiredSkillId.HasValue)
            .Select(r => r.RequiredSkillId!.Value)
            .Distinct()
            .ToList();

        var skillsMap = new Dictionary<Guid, Skill>();
        if (skillIds.Count > 0)
        {
            var skills = await _db.Skills.AsNoTracking()
                .Where(s => skillIds.Contains(s.Id) && s.IsActive && s.DeletedAt == null)
                .ToListAsync(cancellationToken);

            if (skills.Count != skillIds.Count)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "One or more required skills were not found or are inactive.");
            }

            skillsMap = skills.ToDictionary(s => s.Id);
        }

        var newRuleEntities = rules.Select(r => new WardStaffingRule
        {
            WardId = wardId,
            RequiredRole = r.RequiredRole,
            RequiredSkillId = r.RequiredSkillId,
            MinimumHeadcount = r.MinimumHeadcount
        }).ToList();

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var existingRules = await _db.WardStaffingRules
                .Where(r => r.WardId == wardId)
                .ToListAsync(cancellationToken);

            _db.WardStaffingRules.RemoveRange(existingRules);
            _db.WardStaffingRules.AddRange(newRuleEntities);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureShifts = await _db.Shifts
            .AsNoTracking()
            .Include(s => s.RequiredSkill)
            .Include(s => s.Allocations)
            .Where(s => s.WardId == wardId && s.Date >= today)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var shiftsNowDisagreeing = new List<ShiftSummaryDto>();
        foreach (var shift in futureShifts)
        {
            var matchingRule = newRuleEntities.FirstOrDefault(r =>
                r.RequiredRole == shift.RequiredRole &&
                r.RequiredSkillId == shift.RequiredSkillId);

            if (matchingRule != null && shift.MinimumHeadcount != matchingRule.MinimumHeadcount)
            {
                shiftsNowDisagreeing.Add(MapToSummaryDto(shift, ward.Name));
            }
        }

        var ruleDtos = newRuleEntities.Select(r => new WardStaffingRuleDto
        {
            Id = r.Id,
            WardId = r.WardId,
            RequiredRole = r.RequiredRole,
            RequiredSkillId = r.RequiredSkillId,
            RequiredSkillName = r.RequiredSkillId.HasValue && skillsMap.TryGetValue(r.RequiredSkillId.Value, out var sk) ? sk.Name : null,
            MinimumHeadcount = r.MinimumHeadcount
        }).ToList();

        return new ReplaceWardStaffingRulesResponse
        {
            Rules = ruleDtos,
            ShiftsNowDisagreeing = shiftsNowDisagreeing
        };
    }

    private static TimeOnly ParseTime(string timeStr, string fieldName)
    {
        if (TimeOnly.TryParseExact(timeStr, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        throw new BadRequestException(MessageCode.ValidationFailed, $"{fieldName} must be in HH:mm 24-hour format.");
    }

    private async Task<int> DeriveMinimumHeadcountAsync(
        Guid wardId,
        StaffRole role,
        Guid? skillId,
        int headcountNeeded,
        CancellationToken ct)
    {
        var rules = await _db.WardStaffingRules.AsNoTracking()
            .Where(r => r.WardId == wardId && r.RequiredRole == role)
            .ToListAsync(ct);

        WardStaffingRule? matchingRule = null;
        if (skillId.HasValue)
        {
            matchingRule = rules.FirstOrDefault(r => r.RequiredSkillId == skillId.Value);
        }

        matchingRule ??= rules.FirstOrDefault(r => r.RequiredSkillId == null);

        var minimum = matchingRule != null ? matchingRule.MinimumHeadcount : headcountNeeded;
        return Math.Clamp(minimum, 1, headcountNeeded);
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

    private static ShiftSummaryDto MapToSummaryDto(Shift shift, string wardName)
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
            Coverage = ComputeCoverage(shift)
        };
    }
}
