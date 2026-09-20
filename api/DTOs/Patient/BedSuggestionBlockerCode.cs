namespace CareLanka.Api.DTOs.Patient;

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
