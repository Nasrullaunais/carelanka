using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class AmbulanceEligibilityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_failed_invariant_is_explained()
    {
        var service = Service(minimumCrew: 2, locationMaxAgeMinutes: 5);

        var decision = service.Decide(new AmbulanceEligibilityFacts(
            IsActive: false,
            Status: AmbulanceStatus.OutOfService,
            CurrentCrewCount: 1,
            ActiveDispatchId: Guid.NewGuid(),
            HasLocation: false,
            LocationUpdatedAt: null));

        Assert.False(decision.IsEligible);
        Assert.Equal(2, decision.RequiredCrewCount);
        Assert.Equal(
            [
                AmbulanceEligibilityBlockReason.Inactive,
                AmbulanceEligibilityBlockReason.OutOfService,
                AmbulanceEligibilityBlockReason.InsufficientCrew,
                AmbulanceEligibilityBlockReason.ActiveDispatch,
                AmbulanceEligibilityBlockReason.MissingLocation
            ],
            decision.BlockReasons);
    }

    [Fact]
    public void A_location_older_than_the_configured_limit_is_stale()
    {
        var service = Service(minimumCrew: 2, locationMaxAgeMinutes: 5);

        var decision = service.Decide(new AmbulanceEligibilityFacts(
            IsActive: true,
            Status: AmbulanceStatus.Available,
            CurrentCrewCount: 2,
            ActiveDispatchId: null,
            HasLocation: true,
            LocationUpdatedAt: Now.AddMinutes(-6)));

        Assert.Equal([AmbulanceEligibilityBlockReason.StaleLocation], decision.BlockReasons);
    }

    [Fact]
    public void Eligibility_uses_invariants_instead_of_trusting_the_status_projection()
    {
        var service = Service(minimumCrew: 3, locationMaxAgeMinutes: 5);

        var decision = service.Decide(new AmbulanceEligibilityFacts(
            IsActive: true,
            Status: AmbulanceStatus.Dispatched,
            CurrentCrewCount: 3,
            ActiveDispatchId: null,
            HasLocation: true,
            LocationUpdatedAt: Now));

        Assert.True(decision.IsEligible);
        Assert.Equal(3, decision.RequiredCrewCount);
        Assert.Empty(decision.BlockReasons);
    }

    private static AmbulanceEligibilityService Service(int minimumCrew, int locationMaxAgeMinutes)
        => new(
            Options.Create(new EmergencyOptions
            {
                MinimumReadyCrew = minimumCrew,
                LocationMaxAgeMinutes = locationMaxAgeMinutes
            }),
            new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
