using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyReportService(CareLankaDbContext db) : IEmergencyReportService
{
    public async Task<ResponseTimeReport> GetResponseTimesAsync(DateOnly from, DateOnly to, CallPriority? priority,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = Range(from, to);
        var calls = await db.EmergencyCalls.AsNoTracking()
            .Where(call => call.CreatedAt >= start && call.CreatedAt < end && (!priority.HasValue || call.Priority == priority))
            .Select(call => new
            {
                call.Priority,
                call.CreatedAt,
                Dispatches = call.Dispatches.OrderBy(dispatch => dispatch.DispatchedAt).Select(dispatch => new
                {
                    dispatch.DispatchedAt,
                    ArrivedAt = dispatch.RouteLog == null ? null : dispatch.RouteLog.ArrivedAt
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var samples = calls.Where(call => call.Dispatches.Count > 0).Select(call =>
        {
            var firstDispatch = call.Dispatches[0];
            var arrivedDispatch = call.Dispatches.FirstOrDefault(dispatch => dispatch.ArrivedAt.HasValue);
            return new ResponseSample(
                call.Priority,
                (firstDispatch.DispatchedAt - call.CreatedAt).TotalMinutes,
                arrivedDispatch is null ? null : (arrivedDispatch.ArrivedAt!.Value - arrivedDispatch.DispatchedAt).TotalMinutes);
        }).ToList();
        var priorities = priority.HasValue ? [priority.Value] : Enum.GetValues<CallPriority>();
        var rows = priorities.Select(value => Row(value, samples.Where(sample => sample.Priority == value).ToList())).ToList();
        return new ResponseTimeReport
        {
            From = from,
            To = to,
            Rows = rows,
            Totals = new ResponseTimeReportTotals
            {
                CallCount = samples.Count,
                MedianMinutesToDispatch = Median(samples.Select(sample => sample.MinutesToDispatch)),
                MedianMinutesToArrival = Median(samples.Where(sample => sample.MinutesToArrival.HasValue).Select(sample => sample.MinutesToArrival!.Value))
            }
        };
    }

    public async Task<FleetUtilisationReport> GetFleetUtilisationAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = Range(from, to);
        var periodHours = (end - start).TotalHours;
        var ambulances = await db.Ambulances.IgnoreQueryFilters().AsNoTracking()
            .Select(ambulance => new
            {
                ambulance.Id,
                ambulance.RegistrationNumber,
                Runs = ambulance.Dispatches.Where(dispatch => dispatch.DispatchedAt < end &&
                    (dispatch.CompletedAt ?? end) > start).Select(dispatch => new
                    { dispatch.DispatchedAt, dispatch.CompletedAt }).ToList(),
                StatusHistory = ambulance.StatusHistory.Where(history => history.StartedAt < end)
                    .OrderBy(history => history.StartedAt)
                    .Select(history => new StatusInterval(history.Status, history.StartedAt)).ToList()
            }).ToListAsync(cancellationToken);
        return new FleetUtilisationReport
        {
            From = from,
            To = to,
            Rows = ambulances.Select(ambulance =>
            {
                var committed = ambulance.Runs.Sum(run => (
                    ((run.CompletedAt ?? end) < end ? run.CompletedAt!.Value : end) -
                    (run.DispatchedAt > start ? run.DispatchedAt : start)).TotalHours);
                var available = StatusHours(ambulance.StatusHistory, AmbulanceStatus.Available, start, end);
                var outOfService = StatusHours(ambulance.StatusHistory, AmbulanceStatus.OutOfService, start, end);
                return new FleetUtilisationReportRow
                {
                    AmbulanceId = ambulance.Id,
                    RegistrationNumber = ambulance.RegistrationNumber,
                    RunCount = ambulance.Runs.Count,
                    HoursCommitted = Round(committed),
                    IdleShare = Round(Math.Clamp(available / periodHours, 0, 1)),
                    OutOfServiceHours = Round(outOfService)
                };
            }).OrderBy(row => row.RegistrationNumber).ToList()
        };
    }

    public async Task<EmergencyAgentPerformanceReport> GetAgentPerformanceAsync(DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = Range(from, to);
        var proposals = await db.DispatchProposals.AsNoTracking()
            .Where(proposal => proposal.CreatedAt >= start && proposal.CreatedAt < end)
            .Select(proposal => new
            {
                proposal.WorkflowId, proposal.EmergencyCallId, proposal.IsDiversion, proposal.Status, proposal.Outcome,
                proposal.RejectionReason, proposal.CreatedAt, proposal.ReviewedAt, proposal.ResultingDispatchId
            }).ToListAsync(cancellationToken);
        var workflowIds = proposals.Select(proposal => proposal.WorkflowId).ToList();
        var validations = await db.AgentWorkflows.AsNoTracking()
            .Where(workflow => workflowIds.Contains(workflow.Id))
            .Select(workflow => workflow.ValidationResults).ToListAsync(cancellationToken);
        var routine = proposals.Where(proposal => !proposal.IsDiversion).ToList();
        var confirmed = routine.Where(proposal => proposal.Status == DispatchProposalStatus.Executed).ToList();
        var reviewedRoutine = routine.Count(proposal => proposal.Status is
            DispatchProposalStatus.Executed or DispatchProposalStatus.Rejected);
        var dispatchIds = proposals.Where(proposal => proposal.ResultingDispatchId.HasValue)
            .Select(proposal => proposal.ResultingDispatchId!.Value).ToList();
        var dispatches = await db.Dispatches.AsNoTracking().Where(dispatch => dispatchIds.Contains(dispatch.Id))
            .Select(dispatch => new { dispatch.Id, dispatch.DispatchedAt, dispatch.EmergencyCall.CreatedAt }).ToListAsync(cancellationToken);
        var failedValidationCount = validations.Count(json =>
            (DispatchWorkflowJson.Read<List<DispatchValidationResult>>(json) ?? []).Any(result => !result.Passed));
        return new EmergencyAgentPerformanceReport
        {
            From = from,
            To = to,
            ProposalsRaised = proposals.Count,
            Confirmed = confirmed.Count,
            ConfirmedWithoutChangeRate = Ratio(confirmed.Count, reviewedRoutine),
            DiversionsProposed = proposals.Count(proposal => proposal.IsDiversion),
            DiversionsApproved = proposals.Count(proposal => proposal.IsDiversion && proposal.Status == DispatchProposalStatus.Executed),
            DiversionsRejected = proposals.Count(proposal => proposal.IsDiversion && proposal.Status == DispatchProposalStatus.Rejected),
            NoAmbulanceAvailableCount = proposals.Count(proposal => proposal.Outcome == DispatchOutcome.NoAmbulanceAvailable),
            ValidationFailureRate = Ratio(failedValidationCount, proposals.Count),
            MedianSecondsProposalToConfirm = Median(confirmed.Where(proposal => proposal.ReviewedAt.HasValue)
                .Select(proposal => (proposal.ReviewedAt!.Value - proposal.CreatedAt).TotalSeconds)),
            MedianMinutesCallToDispatch = Median(dispatches.Select(dispatch =>
                (dispatch.DispatchedAt - dispatch.CreatedAt).TotalMinutes)),
            RejectionReasons = proposals.Where(proposal => proposal.RejectionReason.HasValue)
                .GroupBy(proposal => EnumWire.ToWire(proposal.RejectionReason!.Value))
                .ToDictionary(group => group.Key, group => group.Count())
        };
    }

    private static ResponseTimeReportRow Row(CallPriority priority, IReadOnlyCollection<ResponseSample> samples)
    {
        var arrivals = samples.Where(sample => sample.MinutesToArrival.HasValue).Select(sample => sample.MinutesToArrival!.Value).ToList();
        return new ResponseTimeReportRow
        {
            Priority = priority,
            CallCount = samples.Count,
            MedianMinutesToDispatch = Median(samples.Select(sample => sample.MinutesToDispatch)),
            MedianMinutesToArrival = Median(arrivals),
            SlowestMinutesToArrival = Round(arrivals.Count == 0 ? 0 : arrivals.Max())
        };
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Range(DateOnly from, DateOnly to)
    {
        if (to < from) throw new BadRequestException(MessageCode.ValidationFailed);
        return (new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) return 0;
        var middle = sorted.Length / 2;
        return Round(sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2);
    }

    private static double Ratio(int numerator, int denominator) => denominator == 0 ? 0 : Round((double)numerator / denominator);
    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static double StatusHours(IReadOnlyList<StatusInterval> history, AmbulanceStatus status,
        DateTimeOffset start, DateTimeOffset end)
    {
        var hours = 0d;
        for (var index = 0; index < history.Count; index++)
        {
            if (history[index].Status != status) continue;
            var intervalStart = history[index].StartedAt > start ? history[index].StartedAt : start;
            var nextStart = index + 1 < history.Count ? history[index + 1].StartedAt : end;
            var intervalEnd = nextStart < end ? nextStart : end;
            if (intervalEnd > intervalStart) hours += (intervalEnd - intervalStart).TotalHours;
        }
        return hours;
    }
    private sealed record ResponseSample(CallPriority Priority, double MinutesToDispatch, double? MinutesToArrival);
    private sealed record StatusInterval(AmbulanceStatus Status, DateTimeOffset StartedAt);
}
