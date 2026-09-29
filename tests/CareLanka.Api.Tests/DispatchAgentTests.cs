using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Data.Enums;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class DispatchAgentTests
{
    [Fact]
    public async Task Excluded_ambulances_are_not_proposed_for_diversion()
    {
        var tools = new FakeTools();
        var result = await Agent(tools).RunAsync(Request([tools.Active.AmbulanceId]));
        Assert.Equal(DispatchOutcome.NoAmbulanceAvailable, result.Outcome);
    }

    [Fact]
    public async Task Diversion_does_not_invent_wait_times_or_a_replacement()
    {
        var result = await Agent(new FakeTools()).RunAsync(Request([]));
        Assert.Equal(DispatchOutcome.DiversionProposed, result.Outcome);
        Assert.Null(result.DiversionImpact!.SourceCallAdditionalWaitMinutes);
        Assert.Null(result.DiversionImpact.MinutesSavedForThisCall);
        Assert.Contains(result.Validation, check => check.Check == "source_call_has_replacement" && !check.Passed);
    }

    [Fact]
    public async Task Unavailable_routes_do_not_claim_the_selected_ambulance_is_nearest()
    {
        var tools = new FakeTools { Free = [Candidate("TEST-02")] };
        var result = await Agent(tools).RunAsync(Request([]));
        Assert.Equal(DispatchOutcome.FreeAmbulanceProposed, result.Outcome);
        Assert.Null(result.EstimatedMinutesToScene);
        Assert.Contains("road estimates are unavailable", result.Rationale);
    }

    [Fact]
    public async Task A_model_pick_within_the_margin_is_proposed_with_its_reason()
    {
        var fast = Candidate("FAST-01");
        var fuller = Candidate("FULL-02");
        var tools = new FakeTools { Free = [fast, fuller], Minutes = { [fast.Id] = 6, [fuller.Id] = 8 } };
        var advisor = new FixedAdvisor(new DispatchAdvice(fuller.Id, "FULL-02 is 8 minutes away with a full crew."));

        var result = await Agent(tools, advisor).RunAsync(Request([]));

        Assert.Equal(fuller.Id, result.ProposedAmbulanceId);
        Assert.Equal(8, result.EstimatedMinutesToScene);
        Assert.Equal(DispatchRecommendationSource.Model, result.RecommendationSource);
        Assert.Contains(result.ToolCalls, call => call.ToolName == "draft_recommendation" && call.Succeeded);
    }

    [Fact]
    public async Task A_model_pick_beyond_the_margin_is_replaced_by_the_fastest()
    {
        var fast = Candidate("FAST-01");
        var slow = Candidate("SLOW-02");
        var tools = new FakeTools { Free = [fast, slow], Minutes = { [fast.Id] = 6, [slow.Id] = 15 } };
        var advisor = new FixedAdvisor(new DispatchAdvice(slow.Id, "SLOW-02 has a full crew."));

        var result = await Agent(tools, advisor).RunAsync(Request([]));

        Assert.Equal(fast.Id, result.ProposedAmbulanceId);
        Assert.Equal(DispatchRecommendationSource.ModelRejected, result.RecommendationSource);
        Assert.Contains("9 minute(s) slower", result.RecommendationNote);
        Assert.Contains("FAST-01", result.Rationale);
    }

    [Fact]
    public async Task The_model_is_never_offered_an_ineligible_ambulance()
    {
        var eligible = Candidate("OK-01");
        var tools = new FakeTools { Free = [eligible] };
        var advisor = new FixedAdvisor(new DispatchAdvice(eligible.Id, "OK-01 is the only option."));

        await Agent(tools, advisor).RunAsync(Request([]));

        Assert.Equal([eligible.Id], advisor.Offered!.Shortlist.Select(ambulance => ambulance.Id));
    }

    private static DispatchAgent Agent(FakeTools tools, IDispatchAdvisor? advisor = null)
        => new(tools, advisor ?? new DeterministicDispatchAdvisor(), TimeProvider.System);

    private static DispatchAgentRequest Request(IReadOnlyList<Guid> excluded) =>
        new(Guid.NewGuid(), CallPriority.Critical, 6.9m, 79.8m, true, excluded);

    private static EligibleAmbulanceCandidate Candidate(string registration) =>
        new(Guid.NewGuid(), registration, 6.9m, 79.8m, 2, DateTimeOffset.UtcNow);

    private sealed class FixedAdvisor(DispatchAdvice advice) : IDispatchAdvisor
    {
        public DispatchChoiceContext? Offered { get; private set; }

        public Task<DispatchAdvice> ChooseAsync(DispatchChoiceContext context, CancellationToken ct = default)
        {
            Offered = context;
            return Task.FromResult(advice);
        }

        public Task<DispatchAdvice> ExplainDiversionAsync(DiversionContext context, CancellationToken ct = default)
            => Task.FromResult(advice);
    }

    private sealed class FakeTools : IDispatchAgentTools
    {
        public IReadOnlyList<EligibleAmbulanceCandidate> Free { get; init; } = [];
        public Dictionary<Guid, int?> Minutes { get; } = [];
        public DivertibleDispatchCandidate Active { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "TEST-01", Guid.NewGuid(), CallPriority.Low, "Test scene", DispatchStatus.Assigned, DateTimeOffset.UtcNow.AddMinutes(-5));
        public Task<IReadOnlyList<EligibleAmbulanceCandidate>> ListEligibleAmbulancesAsync(IReadOnlyCollection<Guid> excludeAmbulanceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(Free);
        public Task<IReadOnlyList<DivertibleDispatchCandidate>> GetActiveDispatchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DivertibleDispatchCandidate>>([Active]);
        public Task<IReadOnlyDictionary<Guid, int?>> GetRouteMinutesAsync(IReadOnlyCollection<EligibleAmbulanceCandidate> ambulances, decimal destinationLatitude, decimal destinationLongitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int?>>(Minutes);
    }
}
