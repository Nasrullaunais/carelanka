using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class StaffReportsService : IStaffReportsService
{
    private static readonly HashSet<string> AllowedLeaveGroupBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "staff",
        "type",
        "ward",
        "month"
    };

    private readonly CareLankaDbContext _db;

    public StaffReportsService(CareLankaDbContext db)
    {
        _db = db;
    }

    public async Task<CoverageReport> GetCoverageReportAsync(
        CoverageReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.From.HasValue || !parameters.To.HasValue)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "From and To dates are required.");
        }

        if (parameters.To.Value < parameters.From.Value)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
        }

        var from = parameters.From.Value;
        var to = parameters.To.Value;

        var shiftQuery = _db.Shifts
            .AsNoTracking()
            .Include(s => s.Allocations)
            .Where(s => s.Date >= from && s.Date <= to);

        if (parameters.WardId.HasValue)
        {
            shiftQuery = shiftQuery.Where(s => s.WardId == parameters.WardId.Value);
        }

        var shifts = await shiftQuery.ToListAsync(cancellationToken);

        var wardIds = shifts.Select(s => s.WardId).Distinct().ToList();
        if (parameters.WardId.HasValue && !wardIds.Contains(parameters.WardId.Value))
        {
            wardIds.Add(parameters.WardId.Value);
        }

        var wardMap = await _db.Wards
            .AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var grouped = shifts
            .GroupBy(s => new { s.WardId, s.Date })
            .ToList();

        var rows = new List<CoverageReportRow>(grouped.Count);

        foreach (var group in grouped)
        {
            var wardId = group.Key.WardId;
            var date = group.Key.Date;
            var wardName = wardMap.TryGetValue(wardId, out var name) ? name : string.Empty;

            var shiftsTotal = group.Count();
            var shiftsUnderstaffed = 0;
            var hoursBelowMinimum = 0.0;
            var groupConfirmed = 0;
            var groupHeadcountNeeded = 0;

            foreach (var shift in group)
            {
                var confirmedCount = shift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed);
                var isUnderstaffed = confirmedCount < shift.MinimumHeadcount;

                groupConfirmed += confirmedCount;
                groupHeadcountNeeded += shift.HeadcountNeeded;

                if (isUnderstaffed)
                {
                    shiftsUnderstaffed++;
                    hoursBelowMinimum += GetShiftDurationHours(shift);
                }
            }

            var fillRate = groupHeadcountNeeded > 0
                ? Math.Clamp((double)groupConfirmed / groupHeadcountNeeded, 0.0, 1.0)
                : 0.0;

            rows.Add(new CoverageReportRow
            {
                WardId = wardId,
                WardName = wardName,
                Date = date,
                ShiftsTotal = shiftsTotal,
                ShiftsUnderstaffed = shiftsUnderstaffed,
                HoursBelowMinimum = hoursBelowMinimum,
                FillRate = fillRate
            });
        }

        var sortedRows = rows
            .OrderBy(r => r.WardName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Date)
            .ToList();

        var totalShifts = sortedRows.Sum(r => r.ShiftsTotal);
        var totalUnderstaffed = sortedRows.Sum(r => r.ShiftsUnderstaffed);
        var totalHoursBelow = sortedRows.Sum(r => r.HoursBelowMinimum);

        var allConfirmed = shifts.Sum(s => s.Allocations.Count(a => a.Status == AllocationStatus.Confirmed));
        var allHeadcountNeeded = shifts.Sum(s => s.HeadcountNeeded);
        var totalFillRate = allHeadcountNeeded > 0
            ? Math.Clamp((double)allConfirmed / allHeadcountNeeded, 0.0, 1.0)
            : 0.0;

        var totals = new CoverageReportTotals
        {
            ShiftsTotal = totalShifts,
            ShiftsUnderstaffed = totalUnderstaffed,
            HoursBelowMinimum = totalHoursBelow,
            FillRate = totalFillRate
        };

        return new CoverageReport
        {
            From = from,
            To = to,
            Rows = sortedRows,
            Totals = totals
        };
    }

    public async Task<LeaveReport> GetLeaveReportAsync(
        LeaveReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.From.HasValue || !parameters.To.HasValue)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "From and To dates are required.");
        }

        if (parameters.To.Value < parameters.From.Value)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
        }

        var normalizedGroupBy = string.IsNullOrWhiteSpace(parameters.GroupBy)
            ? "type"
            : parameters.GroupBy.Trim().ToLowerInvariant();

        if (!AllowedLeaveGroupBy.Contains(normalizedGroupBy))
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "groupBy must be one of: staff, type, ward, month.");
        }

        var from = parameters.From.Value;
        var to = parameters.To.Value;

        var candidateLeaves = await _db.LeaveRequests
            .AsNoTracking()
            .Include(l => l.SwapShift)
            .Where(l => l.StartDate <= to && l.EndDate >= from && l.Status != LeaveStatus.Withdrawn)
            .ToListAsync(cancellationToken);

        if (candidateLeaves.Count == 0)
        {
            return new LeaveReport
            {
                From = from,
                To = to,
                GroupBy = normalizedGroupBy,
                Rows = Array.Empty<LeaveReportRow>()
            };
        }

        var staffMemberIds = candidateLeaves.Select(l => l.StaffMemberId).Distinct().ToList();
        var staffMap = await _db.StaffMembers
            .AsNoTracking()
            .Where(s => staffMemberIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        List<LeaveReportRow> rows;

        switch (normalizedGroupBy)
        {
            case "staff":
                rows = BuildStaffGroupBy(candidateLeaves, staffMap, from, to);
                break;
            case "ward":
                rows = await BuildWardGroupByAsync(candidateLeaves, staffMap, from, to, cancellationToken);
                break;
            case "month":
                rows = BuildMonthGroupBy(candidateLeaves, from, to);
                break;
            case "type":
            default:
                rows = BuildTypeGroupBy(candidateLeaves, from, to);
                break;
        }

        return new LeaveReport
        {
            From = from,
            To = to,
            GroupBy = normalizedGroupBy,
            Rows = rows
        };
    }

    private static List<LeaveReportRow> BuildTypeGroupBy(
        List<LeaveRequest> leaves,
        DateOnly from,
        DateOnly to)
    {
        var dict = new Dictionary<string, (double Approved, double Pending, int Rejected, double Sick)>(StringComparer.OrdinalIgnoreCase);

        foreach (var leave in leaves)
        {
            var overlapStart = leave.StartDate > from ? leave.StartDate : from;
            var overlapEnd = leave.EndDate < to ? leave.EndDate : to;
            if (overlapStart > overlapEnd) continue;

            var overlapDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);
            var key = EnumWire.ToWire(leave.Type);

            dict.TryGetValue(key, out var acc);

            if (leave.Status == LeaveStatus.Approved) acc.Approved += overlapDays;
            if (leave.Status == LeaveStatus.Pending) acc.Pending += overlapDays;
            if (leave.Status == LeaveStatus.Rejected) acc.Rejected += 1;
            if (leave.Type == LeaveType.Sick) acc.Sick += overlapDays;

            dict[key] = acc;
        }

        return dict
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new LeaveReportRow
            {
                Key = kvp.Key,
                ApprovedDays = kvp.Value.Approved,
                PendingDays = kvp.Value.Pending,
                RejectedCount = kvp.Value.Rejected,
                SickDays = kvp.Value.Sick
            })
            .ToList();
    }

    private static List<LeaveReportRow> BuildStaffGroupBy(
        List<LeaveRequest> leaves,
        Dictionary<Guid, StaffMember> staffMap,
        DateOnly from,
        DateOnly to)
    {
        var dict = new Dictionary<string, (double Approved, double Pending, int Rejected, double Sick)>(StringComparer.OrdinalIgnoreCase);

        foreach (var leave in leaves)
        {
            var overlapStart = leave.StartDate > from ? leave.StartDate : from;
            var overlapEnd = leave.EndDate < to ? leave.EndDate : to;
            if (overlapStart > overlapEnd) continue;

            var overlapDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);
            var staff = staffMap.TryGetValue(leave.StaffMemberId, out var s) ? s : null;
            var key = staff?.FullName ?? "Unknown";

            dict.TryGetValue(key, out var acc);

            if (leave.Status == LeaveStatus.Approved) acc.Approved += overlapDays;
            if (leave.Status == LeaveStatus.Pending) acc.Pending += overlapDays;
            if (leave.Status == LeaveStatus.Rejected) acc.Rejected += 1;
            if (leave.Type == LeaveType.Sick) acc.Sick += overlapDays;

            dict[key] = acc;
        }

        return dict
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new LeaveReportRow
            {
                Key = kvp.Key,
                ApprovedDays = kvp.Value.Approved,
                PendingDays = kvp.Value.Pending,
                RejectedCount = kvp.Value.Rejected,
                SickDays = kvp.Value.Sick
            })
            .ToList();
    }

    private async Task<List<LeaveReportRow>> BuildWardGroupByAsync(
        List<LeaveRequest> leaves,
        Dictionary<Guid, StaffMember> staffMap,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var staffIds = leaves.Select(l => l.StaffMemberId).Distinct().ToList();

        var allocations = await _db.Allocations
            .AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => staffIds.Contains(a.StaffMemberId)
                && a.Shift.Date >= from.AddDays(-1)
                && a.Shift.Date <= to)
            .ToListAsync(cancellationToken);

        var wardIds = allocations.Select(a => a.Shift.WardId)
            .Union(leaves.Where(l => l.SwapShift != null).Select(l => l.SwapShift!.WardId))
            .Distinct()
            .ToList();

        var wardMap = await _db.Wards
            .AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var dict = new Dictionary<string, (double Approved, double Pending, int Rejected, double Sick)>(StringComparer.OrdinalIgnoreCase);

        foreach (var leave in leaves)
        {
            var overlapStart = leave.StartDate > from ? leave.StartDate : from;
            var overlapEnd = leave.EndDate < to ? leave.EndDate : to;
            if (overlapStart > overlapEnd) continue;

            var overlapDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);

            string wardKey;
            if (leave.Type == LeaveType.ShiftSwap && leave.SwapShift != null)
            {
                wardKey = wardMap.TryGetValue(leave.SwapShift.WardId, out var wName) ? wName : "Unknown";
            }
            else
            {
                var overlappingAllocations = allocations.Where(a =>
                    a.StaffMemberId == leave.StaffMemberId
                    && a.Shift.Date >= overlapStart
                    && a.Shift.Date <= overlapEnd).ToList();

                if (overlappingAllocations.Count > 0)
                {
                    var shiftWards = overlappingAllocations.Select(a => a.Shift.WardId).Distinct().ToList();
                    if (shiftWards.Count == 1)
                    {
                        wardKey = wardMap.TryGetValue(shiftWards[0], out var wName) ? wName : "Unknown";
                    }
                    else
                    {
                        var allocsByWard = overlappingAllocations.GroupBy(a => a.Shift.WardId).ToList();
                        foreach (var wardGroup in allocsByWard)
                        {
                            var wKey = wardMap.TryGetValue(wardGroup.Key, out var wn) ? wn : "Unknown";
                            var shiftDates = wardGroup.Select(a => a.Shift.Date).Distinct().Count();
                            var wardDays = Math.Min(overlapDays, (double)shiftDates);

                            dict.TryGetValue(wKey, out var wAcc);
                            if (leave.Status == LeaveStatus.Approved) wAcc.Approved += wardDays;
                            if (leave.Status == LeaveStatus.Pending) wAcc.Pending += wardDays;
                            if (leave.Status == LeaveStatus.Rejected) wAcc.Rejected += 1;
                            if (leave.Type == LeaveType.Sick) wAcc.Sick += wardDays;
                            dict[wKey] = wAcc;
                        }
                        continue;
                    }
                }
                else
                {
                    var staff = staffMap.TryGetValue(leave.StaffMemberId, out var s) ? s : null;
                    wardKey = !string.IsNullOrWhiteSpace(staff?.Department) ? staff.Department : "Unassigned";
                }
            }

            dict.TryGetValue(wardKey, out var acc);

            if (leave.Status == LeaveStatus.Approved) acc.Approved += overlapDays;
            if (leave.Status == LeaveStatus.Pending) acc.Pending += overlapDays;
            if (leave.Status == LeaveStatus.Rejected) acc.Rejected += 1;
            if (leave.Type == LeaveType.Sick) acc.Sick += overlapDays;

            dict[wardKey] = acc;
        }

        return dict
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new LeaveReportRow
            {
                Key = kvp.Key,
                ApprovedDays = kvp.Value.Approved,
                PendingDays = kvp.Value.Pending,
                RejectedCount = kvp.Value.Rejected,
                SickDays = kvp.Value.Sick
            })
            .ToList();
    }

    private static List<LeaveReportRow> BuildMonthGroupBy(
        List<LeaveRequest> leaves,
        DateOnly from,
        DateOnly to)
    {
        var dict = new Dictionary<string, (double Approved, double Pending, int Rejected, double Sick)>(StringComparer.OrdinalIgnoreCase);

        var current = new DateOnly(from.Year, from.Month, 1);
        var endMonth = new DateOnly(to.Year, to.Month, 1);

        while (current <= endMonth)
        {
            var year = current.Year;
            var month = current.Month;
            var monthKey = $"{year:D4}-{month:D2}";

            var firstDay = new DateOnly(year, month, 1);
            var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

            var monthWindowStart = firstDay > from ? firstDay : from;
            var monthWindowEnd = lastDay < to ? lastDay : to;

            double approved = 0;
            double pending = 0;
            int rejected = 0;
            double sick = 0;
            bool hasActivity = false;

            foreach (var leave in leaves)
            {
                var overlapStart = leave.StartDate > monthWindowStart ? leave.StartDate : monthWindowStart;
                var overlapEnd = leave.EndDate < monthWindowEnd ? leave.EndDate : monthWindowEnd;

                if (overlapStart <= overlapEnd)
                {
                    hasActivity = true;
                    var overlapDays = (double)((overlapEnd.DayNumber - overlapStart.DayNumber) + 1);

                    if (leave.Status == LeaveStatus.Approved) approved += overlapDays;
                    if (leave.Status == LeaveStatus.Pending) pending += overlapDays;
                    if (leave.Status == LeaveStatus.Rejected) rejected += 1;
                    if (leave.Type == LeaveType.Sick) sick += overlapDays;
                }
            }

            if (hasActivity)
            {
                dict[monthKey] = (approved, pending, rejected, sick);
            }

            current = current.AddMonths(1);
        }

        return dict
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new LeaveReportRow
            {
                Key = kvp.Key,
                ApprovedDays = kvp.Value.Approved,
                PendingDays = kvp.Value.Pending,
                RejectedCount = kvp.Value.Rejected,
                SickDays = kvp.Value.Sick
            })
            .ToList();
    }

    public static double GetShiftDurationHours(Shift shift)
    {
        if (shift.EndTime > shift.StartTime)
        {
            return (shift.EndTime - shift.StartTime).TotalHours;
        }

        // Overnight shift crosses midnight and ends on Date + 1 day
        return (shift.EndTime.ToTimeSpan() + TimeSpan.FromHours(24) - shift.StartTime.ToTimeSpan()).TotalHours;
    }

    public async Task<StaffAgentPerformanceReport> GetStaffAgentPerformanceReportAsync(
        StaffAgentPerformanceReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.From.HasValue || !parameters.To.HasValue)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "From and To dates are required.");
        }

        if (parameters.To.Value < parameters.From.Value)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
        }

        var from = parameters.From.Value;
        var to = parameters.To.Value;

        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var workflows = await _db.AgentWorkflows
            .AsNoTracking()
            .Where(w => w.AgentType == AgentType.StaffAllocation
                && w.CreatedAt >= fromUtc
                && w.CreatedAt < toUtc)
            .ToListAsync(cancellationToken);

        int proposalsRaised = workflows.Count;

        if (proposalsRaised == 0)
        {
            return new StaffAgentPerformanceReport
            {
                From = from,
                To = to,
                ProposalsRaised = 0,
                ProposalsAutoTriggered = 0,
                ValidationFailureRate = 0.0,
                Approved = 0,
                Rejected = 0,
                RevisionRequested = 0,
                FailedSafely = 0,
                CascadingSwaps = 0,
                MedianMinutesGapToFill = 0.0,
                RejectionReasons = new Dictionary<string, int>()
            };
        }

        var workflowIds = workflows.Select(w => w.Id).ToList();

        var changes = await _db.AgentProposedChanges
            .AsNoTracking()
            .Where(c => workflowIds.Contains(c.AgentWorkflowId))
            .ToListAsync(cancellationToken);

        var changesByWorkflow = changes
            .GroupBy(c => c.AgentWorkflowId)
            .ToDictionary(g => g.Key, g => g.ToList());

        int proposalsAutoTriggered = workflows.Count(w => w.ParentWorkflowId.HasValue);

        var failedValidationWorkflowIds = changes
            .Where(c => c.ValidationStatus == ProposedChangeValidationStatus.Failed)
            .Select(c => c.AgentWorkflowId)
            .Distinct()
            .ToHashSet();

        int failedValidationCount = workflows.Count(w => failedValidationWorkflowIds.Contains(w.Id));
        double validationFailureRate = Math.Round((double)failedValidationCount / proposalsRaised, 4);

        int approved = workflows.Count(w => w.Status == AgentWorkflowStatus.Approved || w.Status == AgentWorkflowStatus.Executed);
        int rejected = workflows.Count(w => w.Status == AgentWorkflowStatus.Rejected);
        int revisionRequested = workflows.Count(w => w.Status == AgentWorkflowStatus.RevisionRequested);
        int failedSafely = workflows.Count(IsFailedSafely);

        int cascadingSwaps = workflows.Count(w =>
        {
            changesByWorkflow.TryGetValue(w.Id, out var wfChanges);
            return IsCascadingSwap(w, wfChanges ?? new List<AgentProposedChange>());
        });

        var durations = new List<double>();
        foreach (var workflow in workflows.Where(w => w.Status == AgentWorkflowStatus.Approved || w.Status == AgentWorkflowStatus.Executed))
        {
            if (changesByWorkflow.TryGetValue(workflow.Id, out var wfChanges))
            {
                var appliedChanges = wfChanges.Where(c => c.AppliedAt.HasValue).ToList();
                if (appliedChanges.Count > 0)
                {
                    var appliedAt = appliedChanges.Min(c => c.AppliedAt!.Value);
                    var duration = (appliedAt - workflow.CreatedAt).TotalMinutes;
                    if (duration >= 0)
                    {
                        durations.Add(duration);
                    }
                }
            }
        }

        durations.Sort();
        double medianMinutesGapToFill = 0.0;
        if (durations.Count > 0)
        {
            int n = durations.Count;
            if (n % 2 == 1)
            {
                medianMinutesGapToFill = durations[n / 2];
            }
            else
            {
                medianMinutesGapToFill = (durations[(n / 2) - 1] + durations[n / 2]) / 2.0;
            }
        }
        medianMinutesGapToFill = Math.Round(medianMinutesGapToFill, 2);

        var rejectionReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var rejectedWorkflow in workflows.Where(w => w.Status == AgentWorkflowStatus.Rejected))
        {
            var reasonWire = ExtractRejectionReasonWire(rejectedWorkflow);
            if (!string.IsNullOrWhiteSpace(reasonWire))
            {
                rejectionReasons.TryGetValue(reasonWire, out var count);
                rejectionReasons[reasonWire] = count + 1;
            }
        }

        var sortedRejectionReasons = rejectionReasons
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new StaffAgentPerformanceReport
        {
            From = from,
            To = to,
            ProposalsRaised = proposalsRaised,
            ProposalsAutoTriggered = proposalsAutoTriggered,
            ValidationFailureRate = validationFailureRate,
            Approved = approved,
            Rejected = rejected,
            RevisionRequested = revisionRequested,
            FailedSafely = failedSafely,
            CascadingSwaps = cascadingSwaps,
            MedianMinutesGapToFill = medianMinutesGapToFill,
            RejectionReasons = sortedRejectionReasons
        };
    }

    private static string? ExtractRejectionReasonWire(AgentWorkflow workflow)
    {
        if (!string.IsNullOrWhiteSpace(workflow.FinalOutcome))
        {
            var match = MatchRejectionReasonWire(workflow.FinalOutcome);
            if (match != null) return match;
        }

        if (!string.IsNullOrWhiteSpace(workflow.ReviewNotes))
        {
            var match = MatchRejectionReasonWire(workflow.ReviewNotes);
            if (match != null) return match;
        }

        return null;
    }

    private static string? MatchRejectionReasonWire(string value)
    {
        var trimmed = value.Trim();
        foreach (var r in Enum.GetValues<RejectionReason>())
        {
            var wire = EnumWire.ToWire(r);
            if (string.Equals(trimmed, wire, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, r.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return wire;
            }
        }
        return null;
    }

    private static bool IsFailedSafely(AgentWorkflow workflow)
    {
        if (workflow.Status == AgentWorkflowStatus.Failed)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(workflow.FinalOutcome))
        {
            var outcome = workflow.FinalOutcome.Trim();
            if (string.Equals(outcome, EnumWire.ToWire(AgentOutcome.NoCandidateFound), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(outcome, nameof(AgentOutcome.NoCandidateFound), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(outcome, EnumWire.ToWire(AgentOutcome.Failed), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(outcome, nameof(AgentOutcome.Failed), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCascadingSwap(AgentWorkflow workflow, List<AgentProposedChange> changes)
    {
        if (string.Equals(workflow.FinalOutcome, EnumWire.ToWire(AgentOutcome.SwapProposed), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflow.FinalOutcome, nameof(AgentOutcome.SwapProposed), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var change in changes)
        {
            if (change.ChangeType == ProposedChangeType.EndAllocation)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(change.Payload))
            {
                if (change.Payload.Contains("\"from_ward\"", StringComparison.OrdinalIgnoreCase) ||
                    change.Payload.Contains("\"is_cascading_swap\":true", StringComparison.OrdinalIgnoreCase) ||
                    change.Payload.Contains("\"is_cascading_swap\": true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
