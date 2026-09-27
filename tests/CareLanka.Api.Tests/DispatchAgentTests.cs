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
        var result = await new DispatchAgent(tools, TimeProvider.System).RunAsync(Request([tools.Active.AmbulanceId]));
        Assert.Equal(DispatchOutcome.NoAmbulanceAvailable, result.Outcome);
    }

    [Fact]
    public async Task Diversion_does_not_invent_wait_times_or_a_replacement()
    {
        var result = await new DispatchAgent(new FakeTools(), TimeProvider.System).RunAsync(Request([]));
        Assert.Equal(DispatchOutcome.DiversionProposed, result.Outcome);
        Assert.Null(result.DiversionImpact!.SourceCallAdditionalWaitMinutes);
        Assert.Null(result.DiversionImpact.MinutesSavedForThisCall);
        Assert.Contains(result.Validation, check => check.Check == "source_call_has_replacement" && !check.Passed);
    }

    [Fact]
    public async Task Unavailable_routes_do_not_claim_the_selected_ambulance_is_nearest()
    {
        var tools = new FakeTools { HasFreeAmbulance = true };
        var result = await new DispatchAgent(tools, TimeProvider.System).RunAsync(Request([]));
        Assert.Equal(DispatchOutcome.FreeAmbulanceProposed, result.Outcome);
        Assert.Null(result.EstimatedMinutesToScene);
        Assert.Contains("road estimates are unavailable", result.Rationale);
    }

    private static DispatchAgentRequest Request(IReadOnlyList<Guid> excluded) =>
        new(Guid.NewGuid(), CallPriority.Critical, 6.9m, 79.8m, true, excluded);

    private sealed class FakeTools : IDispatchAgentTools
    {
        public bool HasFreeAmbulance { get; init; }
        public DivertibleDispatchCandidate Active { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "TEST-01", Guid.NewGuid(), CallPriority.Low, "Test scene", DispatchStatus.Assigned, DateTimeOffset.UtcNow.AddMinutes(-5));
        public Task<IReadOnlyList<EligibleAmbulanceCandidate>> ListEligibleAmbulancesAsync(IReadOnlyCollection<Guid> excludeAmbulanceIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EligibleAmbulanceCandidate>>(HasFreeAmbulance ? [new(Guid.NewGuid(), "TEST-02", 6.9m, 79.8m)] : []);
        public Task<IReadOnlyList<DivertibleDispatchCandidate>> GetActiveDispatchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DivertibleDispatchCandidate>>([Active]);
        public Task<IReadOnlyDictionary<Guid, int?>> GetRouteMinutesAsync(IReadOnlyCollection<EligibleAmbulanceCandidate> ambulances, decimal destinationLatitude, decimal destinationLongitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int?>>(new Dictionary<Guid, int?>());
    }
}
