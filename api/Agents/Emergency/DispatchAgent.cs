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
    private const string RankDiversionsByEta = "rank_diversions_by_eta";
    private const string DraftRecommendation = "draft_recommendation";
    private const string RecommendationWithinRules = "recommendation_within_rules";
    private const int ShortlistSize = 5;
    private const string Decide = "decide_free_or_diversion";
    private const string Validate = "validate_deterministically";
    private const string Pause = "pause_for_approval";

    private readonly IDispatchAgentTools _tools;
    private readonly IDispatchAdvisor _advisor;
    private readonly TimeProvider _clock;

    public DispatchAgent(IDispatchAgentTools tools, IDispatchAdvisor advisor, TimeProvider clock)
    {
        _tools = tools;
        _advisor = advisor;
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
                () => _tools.GetRouteMinutesAsync(
                    eligible.Select(candidate => new AmbulanceLocation(candidate.Id, candidate.Latitude, candidate.Longitude)).ToList(),
                    request.Latitude, request.Longitude, ct));
            Step(plan, RankByEta);

            var now = _clock.GetUtcNow();
            var call = new DispatchCallFacts(request.CallPriority, request.CallDetails, request.PatientKnown);
            var shortlist = eligible
                .Select(candidate => new RankedAmbulance(
                    candidate.Id,
                    candidate.RegistrationNumber,
                    routeMinutes.GetValueOrDefault(candidate.Id),
                    candidate.CrewCount,
                    Math.Max(0, (int)(now - candidate.LocationUpdatedAt).TotalSeconds)))
                .OrderBy(candidate => candidate.RouteMinutes ?? int.MaxValue)
                .Take(ShortlistSize)
                .ToList();

            if (shortlist.Count > 0)
            {
                var context = new DispatchChoiceContext(call, shortlist);
                var drafted = await DraftAsync(
                    toolCalls, new { shortlist = shortlist.Count }, () => _advisor.ChooseAsync(context, ct));
                Step(plan, DraftRecommendation);
                Step(plan, Decide);

                var (advice, adviceCheck) = await CheckAsync(
                    drafted, DispatchAdviceValidator.ValidateChoice(drafted, context),
                    () => new DeterministicDispatchAdvisor().ChooseAsync(context, ct), now);
                var chosen = shortlist.Single(candidate => candidate.Id == advice.AmbulanceId);

                var validation = new List<DispatchValidationResult>
                {
                    adviceCheck,
                    DispatchProposalValidator.AmbulanceEligible(true, now)
                };
                Step(plan, Validate);
                Step(plan, Pause);

                return new DispatchAgentRun(
                    plan, toolCalls, validation, DispatchOutcome.FreeAmbulanceProposed,
                    IsDiversion: false, chosen.Id, chosen.RegistrationNumber, chosen.RouteMinutes,
                    advice.Rationale, DiversionImpact: null, SourceDispatchId: null, errors,
                    advice.Source, advice.SourceNote);
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

            var candidates = active
                .Where(dispatch => dispatch.CallPriority > request.CallPriority
                    && !request.ExcludeAmbulanceIds.Contains(dispatch.AmbulanceId)
                    && dispatch.Status.IsPrePickup())
                .ToList();
            var diversionMinutes = candidates.Count == 0
                ? new Dictionary<Guid, int?>()
                : await CallToolAsync(
                    toolCalls, RankDiversionsByEta, new { candidates = candidates.Count },
                    () => _tools.GetRouteMinutesAsync(
                        candidates.Select(dispatch => new AmbulanceLocation(
                            dispatch.AmbulanceId, dispatch.AmbulanceLatitude, dispatch.AmbulanceLongitude)).ToList(),
                        request.Latitude, request.Longitude, ct));

            // The quickest ambulance to reach this patient; among equals, take it from the least urgent call.
            var divertible = candidates
                .OrderBy(dispatch => diversionMinutes.GetValueOrDefault(dispatch.AmbulanceId) ?? int.MaxValue)
                .ThenByDescending(dispatch => dispatch.CallPriority)
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

            var waitingMinutes = Math.Max(0, (int)(now - divertible.CallCreatedAt).TotalMinutes);
            var minutesToThisCall = diversionMinutes.GetValueOrDefault(divertible.AmbulanceId);

            var diversion = new DiversionContext(call, divertible, waitingMinutes, minutesToThisCall);
            var explained = await DraftAsync(
                toolCalls, new { diverted = divertible.AmbulanceRegistration },
                () => _advisor.ExplainDiversionAsync(diversion, ct));
            Step(plan, DraftRecommendation);

            var (diversionAdvice, diversionCheck) = await CheckAsync(
                explained, DispatchAdviceValidator.ValidateDiversion(explained, diversion),
                () => new DeterministicDispatchAdvisor().ExplainDiversionAsync(diversion, ct), now);

            var diversionValidation = new List<DispatchValidationResult>
            {
                diversionCheck,
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
                IsDiversion: true, divertible.AmbulanceId, divertible.AmbulanceRegistration, minutesToThisCall,
                diversionAdvice.Rationale, impact, divertible.DispatchId, errors,
                diversionAdvice.Source, diversionAdvice.SourceNote);
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

    private async Task<DispatchAdvice> DraftAsync(
        List<DispatchToolCall> calls, object arguments, Func<Task<DispatchAdvice>> draft)
    {
        var startedAt = _clock.GetUtcNow();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var advice = await draft();

        calls.Add(new DispatchToolCall
        {
            ToolName = DraftRecommendation, Arguments = ToArgs(arguments),
            Succeeded = advice.Source == DispatchRecommendationSource.Model,
            DurationMs = (int)stopwatch.ElapsedMilliseconds, Error = advice.SourceNote, CalledAt = startedAt
        });

        return advice;
    }

    private static async Task<(DispatchAdvice Advice, DispatchValidationResult Check)> CheckAsync(
        DispatchAdvice drafted, DispatchAdviceVerdict verdict, Func<Task<DispatchAdvice>> fallback,
        DateTimeOffset now)
    {
        if (verdict.Passed)
        {
            return (drafted, RecommendationCheck(verdict.Detail, now));
        }

        var note = $"{verdict.Detail} The fastest-first rule was used instead.";
        var replacement = (await fallback()) with
        {
            Source = DispatchRecommendationSource.ModelRejected,
            SourceNote = note
        };

        return (replacement, RecommendationCheck(note, now));
    }

    private static DispatchValidationResult RecommendationCheck(string detail, DateTimeOffset now)
        => new() { Check = RecommendationWithinRules, Passed = true, Detail = detail, CheckedAt = now };

    private void Step(List<DispatchPlanStep> plan, string description)
    {
        var now = _clock.GetUtcNow();
        plan.Add(new DispatchPlanStep
        {
            Sequence = plan.Count + 1, Description = description, Status = "completed",
            StartedAt = now, CompletedAt = now
        });
    }

    private async Task<TResult> CallToolAsync<TResult>(
        List<DispatchToolCall> calls, string toolName, object arguments, Func<Task<TResult>> call)
    {
        var startedAt = _clock.GetUtcNow();
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
