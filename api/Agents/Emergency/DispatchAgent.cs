using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Agents.Emergency;

/// <summary>
/// The Dispatch &amp; Routing Agent. One run answers one question - which eligible ambulance should
/// take this call, and why - by listing eligible ambulances, ranking them by driving ETA, and
/// either proposing the nearest free one or, if none is free, looking at pre-pickup runs on
/// lower-priority calls for a diversion candidate.
/// </summary>
/// <remarks>
/// It writes nothing. No tool here changes the database; the dispatch is only created when a Duty
/// Manager confirms or approves through the proposal API.
/// </remarks>
public sealed class DispatchAgent : IDispatchAgent
{
    private const string ReadCall = "read_call";
    private const string ListCandidates = "list_eligible_ambulances";
    private const string RankByEta = "rank_by_eta";
    private const string Decide = "decide_free_or_diversion";
    private const string Validate = "validate_deterministically";
    private const string Pause = "pause_for_approval";

    private readonly IDispatchAgentTools _tools;
    private readonly TimeProvider _clock;

    public DispatchAgent(IDispatchAgentTools tools, TimeProvider clock)
    {
        _tools = tools;
        _clock = clock;
    }

    public async Task<DispatchAgentRun> RunAsync(DispatchAgentRequest request, CancellationToken ct = default)
    {
        var plan = new List<DispatchPlanStep>();
        var toolCalls = new List<DispatchToolCall>();
        var errors = new List<string>();

        try
        {
            Step(plan, ReadCall);

            var eligible = await CallToolAsync(
                toolCalls, ListCandidates, new { exclude = request.ExcludeAmbulanceIds },
                () => _tools.ListEligibleAmbulancesAsync(request.ExcludeAmbulanceIds, ct));
            Step(plan, ListCandidates);

            var routeMinutes = await CallToolAsync(
                toolCalls, RankByEta, new { destination = new { lat = request.Latitude, lon = request.Longitude } },
                () => _tools.GetRouteMinutesAsync(eligible, request.Latitude, request.Longitude, ct));
            Step(plan, RankByEta);

            var ranked = eligible
                .Select(candidate => (candidate, minutes: routeMinutes.GetValueOrDefault(candidate.Id)))
                .OrderBy(row => row.minutes ?? int.MaxValue)
                .ToList();

            if (ranked.Count > 0)
            {
                var (best, minutes) = ranked[0];
                Step(plan, Decide);

                var validation = new List<DispatchValidationResult>
                {
                    DispatchProposalValidator.AmbulanceEligible(true, _clock.GetUtcNow())
                };
                Step(plan, Validate);
                Step(plan, Pause);

                return new DispatchAgentRun(
                    plan, toolCalls, validation, DispatchOutcome.FreeAmbulanceProposed,
                    IsDiversion: false, best.Id, best.RegistrationNumber, minutes,
                    Rationale(minutes is { } m
                        ? $"{best.RegistrationNumber} has the shortest available road estimate among eligible ambulances, about {m} minute(s)."
                        : $"{best.RegistrationNumber} is eligible, but road estimates are unavailable. Compare locations before confirming."),
                    DiversionImpact: null, SourceDispatchId: null, errors);
            }

            if (!request.AllowDiversion)
            {
                Step(plan, Decide);
                Step(plan, Pause);

                return new DispatchAgentRun(
                    plan, toolCalls, [], DispatchOutcome.NoAmbulanceAvailable, false, null, null, null,
                    "No eligible ambulance is free, and diversion was not permitted for this request.",
                    null, null, errors);
            }

            var active = await CallToolAsync(
                toolCalls, "get_active_dispatches", new { }, () => _tools.GetActiveDispatchesAsync(ct));

            var divertible = active
                .Where(dispatch => dispatch.CallPriority > request.CallPriority
                    && !request.ExcludeAmbulanceIds.Contains(dispatch.AmbulanceId)
                    && dispatch.Status.IsPrePickup())
                .OrderByDescending(dispatch => dispatch.CallPriority)
                .ThenBy(dispatch => dispatch.DispatchedAt)
                .FirstOrDefault();

            Step(plan, Decide);

            if (divertible is null)
            {
                Step(plan, Pause);

                return new DispatchAgentRun(
                    plan, toolCalls, [], DispatchOutcome.NoAmbulanceAvailable, false, null, null, null,
                    "No eligible ambulance is free and nothing pre-pickup can be diverted.",
                    null, null, errors);
            }

            var now = _clock.GetUtcNow();
            var waitingMinutes = Math.Max(0, (int)(now - divertible.DispatchedAt).TotalMinutes);

            var diversionValidation = new List<DispatchValidationResult>
            {
                DispatchProposalValidator.SourcePrePickup(divertible.Status, now),
                DispatchProposalValidator.ReplacementAvailable(false, now)
            };
            Step(plan, Validate);
            Step(plan, Pause);

            var impact = new DiversionImpact
            {
                SourceDispatchId = divertible.DispatchId,
                SourceCallId = divertible.CallId,
                SourceCallPriority = divertible.CallPriority,
                SourceCallAddressLabel = divertible.CallAddressLabel,
                SourceDispatchStatus = divertible.Status,
                SourceCallWaitingMinutesSoFar = waitingMinutes,
                SourceCallAdditionalWaitMinutes = null,
                ReplacementAmbulanceId = null,
                ReplacementAmbulanceRegistration = null,
                MinutesSavedForThisCall = null
            };

            return new DispatchAgentRun(
                plan, toolCalls, diversionValidation, DispatchOutcome.DiversionProposed,
                IsDiversion: true, divertible.AmbulanceId, divertible.AmbulanceRegistration, null,
                Rationale($"Every ambulance is committed. {divertible.AmbulanceRegistration} is pre-pickup on a " +
                    $"lower-priority call and can be turned around; that call returns to the queue."),
                impact, divertible.DispatchId, errors);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            errors.Add(failure.Message);

            return new DispatchAgentRun(
                plan, toolCalls, [], DispatchOutcome.Failed, false, null, null, null, null, null, null, errors);
        }
    }

    private static string Rationale(string text) => text;

    private static void Step(List<DispatchPlanStep> plan, string description)
        => plan.Add(new DispatchPlanStep
        {
            Sequence = plan.Count + 1, Description = description, Status = "completed",
            StartedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow
        });

    private async Task<TResult> CallToolAsync<TResult>(
        List<DispatchToolCall> calls, string toolName, object arguments, Func<Task<TResult>> call)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await call();
            calls.Add(new DispatchToolCall
            {
                ToolName = toolName, Arguments = ToArgs(arguments), Succeeded = true,
                DurationMs = (int)stopwatch.ElapsedMilliseconds, CalledAt = startedAt
            });

            return result;
        }
        catch (Exception failure)
        {
            calls.Add(new DispatchToolCall
            {
                ToolName = toolName, Arguments = ToArgs(arguments), Succeeded = false,
                DurationMs = (int)stopwatch.ElapsedMilliseconds, Error = failure.Message, CalledAt = startedAt
            });

            throw;
        }
    }

    private static IReadOnlyDictionary<string, object?> ToArgs(object arguments)
        => System.Text.Json.JsonSerializer.SerializeToElement(arguments)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.ToString());
}
