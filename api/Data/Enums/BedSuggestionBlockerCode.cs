namespace CareLanka.Api.Data.Enums;

/// <summary>
/// Which wall the agent hit. Paired with one plain sentence built in C# from the filter result -
/// never written by the model, because a model asked to explain its own failure writes something
/// plausible rather than something true.
/// </summary>
public enum BedSuggestionBlockerCode
{
    DowngradeNeeded,
    UpgradeOnly,
    WardFull,
    GenderPolicy,
    NeedsIsolation,
    PediatricOnly,
    NoBedRequired,
    NoSuchPatient,
    NoOpenAdmission,
    AgentFailed
}
