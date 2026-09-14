using CareLanka.Api.Data.Enums;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class AmbulanceEligibilityService : IAmbulanceEligibilityService
{
    private readonly EmergencyOptions _options;
    private readonly TimeProvider _timeProvider;

    public AmbulanceEligibilityService(IOptions<EmergencyOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public AmbulanceEligibilityDecision Decide(AmbulanceEligibilityFacts facts)
    {
        var reasons = new List<AmbulanceEligibilityBlockReason>();

        if (!facts.IsActive)
        {
            reasons.Add(AmbulanceEligibilityBlockReason.Inactive);
        }

        if (facts.Status == AmbulanceStatus.OutOfService)
        {
            reasons.Add(AmbulanceEligibilityBlockReason.OutOfService);
        }

        if (facts.CurrentCrewCount < _options.MinimumReadyCrew)
        {
            reasons.Add(AmbulanceEligibilityBlockReason.InsufficientCrew);
        }

        if (facts.ActiveDispatchId.HasValue)
        {
            reasons.Add(AmbulanceEligibilityBlockReason.ActiveDispatch);
        }

        if (!facts.HasLocation || !facts.LocationUpdatedAt.HasValue)
        {
            reasons.Add(AmbulanceEligibilityBlockReason.MissingLocation);
        }
        else if (_timeProvider.GetUtcNow() - facts.LocationUpdatedAt.Value
                 > TimeSpan.FromMinutes(_options.LocationMaxAgeMinutes))
        {
            reasons.Add(AmbulanceEligibilityBlockReason.StaleLocation);
        }

        return new AmbulanceEligibilityDecision(
            reasons.Count == 0,
            _options.MinimumReadyCrew,
            reasons);
    }
}
