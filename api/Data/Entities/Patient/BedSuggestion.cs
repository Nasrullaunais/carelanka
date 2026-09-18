using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

/// <summary>
/// The bed agent's answer for one run: who it decided the patient is, what it decided about a
/// bed, and - on <see cref="Candidates"/> - every bed a human may press a button on.
/// </summary>
/// <remarks>
/// This is Patient Management's table, not the group's. <c>AgentProposedChange</c> holds a domain
/// write waiting to be applied; nothing here is ever applied, because confirming a suggestion runs
/// <c>POST /admissions/{id}/assign-bed</c> - the manual endpoint - and writes its own row.
/// Typed columns rather than a jsonb blob for the reason ADR 3 gives: a bed id inside a blob can
/// point at a bed the register retired an hour ago, and nothing would notice.
/// </remarks>
public class BedSuggestion : AuditedEntity
{
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// The NIC or patient code the caller typed, when they started from a slip rather than a row
    /// on the board. Kept because a trace that cannot say what was asked cannot explain what came
    /// back - a lookup that matched nobody has nothing else to point at.
    /// </summary>
    public string? RequestedIdentifier { get; set; }

    /// <summary>Null until the agent resolves it, and forever if the identifier matched nobody.</summary>
    public Guid? PatientId { get; set; }

    public Patient? Patient { get; set; }

    /// <summary>
    /// The visit this run is about: supplied by the caller, or resolved from the identifier.
    /// </summary>
    public Guid? AdmissionId { get; set; }

    public Admission? Admission { get; set; }

    /// <summary>Null while the run is still going.</summary>
    public BedAgentOutcome? Outcome { get; set; }

    public BedSuggestionBlockerCode? BlockerCode { get; set; }

    public string? BlockerMessage { get; set; }

    // Which role has to press the button is NOT stored here. It lives on
    // AgentWorkflow.RequiredApproverRole, which is where the design doc puts it so the
    // authorization rule is visible in the audit trail. Two copies is two ways to disagree.

    /// <summary>
    /// False when the deterministic re-check dropped something. The answer still renders: what it
    /// dropped never reaches a human, and the verdicts are on the workflow row.
    /// </summary>
    public bool ValidationPassed { get; set; } = true;

    public ICollection<BedSuggestionCandidate> Candidates { get; set; }
        = new List<BedSuggestionCandidate>();
}
