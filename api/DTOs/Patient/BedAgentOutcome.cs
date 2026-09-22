namespace CareLanka.Api.DTOs.Patient;

public enum BedAgentOutcome
{
    Proposed,
    ProposedWithDowngrade,
    NeedsDutyManager,
    NoBedAvailable,
    VisitNeedsNoBed,
    PatientNotFound,
    Failed
}
