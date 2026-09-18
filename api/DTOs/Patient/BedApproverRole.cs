namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Which of the two people may press "Use this bed". A component-specific vocabulary with a
/// component-specific name, rather than publishing the whole group-owned <c>StaffRole</c> to say
/// one of two things. Mapped from <c>AgentWorkflow.RequiredApproverRole</c>, which is where the
/// fact is stored.
/// </summary>
public enum BedApproverRole
{
    WardNurse,
    DutyManager
}
