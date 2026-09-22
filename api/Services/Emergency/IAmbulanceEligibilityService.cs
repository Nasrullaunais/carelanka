using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

public interface IAmbulanceEligibilityService
{
    AmbulanceEligibilityDecision Decide(AmbulanceEligibilityFacts facts);
}

public sealed record AmbulanceEligibilityFacts(
    bool IsActive,
    AmbulanceStatus Status,
    int CurrentCrewCount,
    Guid? ActiveDispatchId,
    bool HasLocation,
    DateTimeOffset? LocationUpdatedAt);

public sealed record AmbulanceEligibilityDecision(
    bool IsEligible,
    int RequiredCrewCount,
    IReadOnlyList<AmbulanceEligibilityBlockReason> BlockReasons);
