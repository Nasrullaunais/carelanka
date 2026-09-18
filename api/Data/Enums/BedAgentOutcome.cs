namespace CareLanka.Api.Data.Enums;

/// <summary>
/// What the bed agent decided. Named <c>BedAgentOutcome</c> rather than <c>AgentOutcome</c>
/// because <c>staff-spec.yaml</c> publishes its own <c>AgentOutcome</c> for a different thing -
/// one app, one set of schema names.
/// </summary>
public enum BedAgentOutcome
{
    Proposed,
    ProposedWithDowngrade,
    NeedsDutyManager,

    /// <summary>The hospital is full for this patient. Different from needing no bed at all.</summary>
    NoBedAvailable,

    VisitNeedsNoBed,

    /// <summary>The NIC or patient code matched nobody. An answer, not an error.</summary>
    PatientNotFound,

    Failed
}
