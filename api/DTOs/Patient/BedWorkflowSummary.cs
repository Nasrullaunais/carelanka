namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Persisted agent execution state. Raw model reasoning is not stored - only the short summary
/// shown on screen.
/// </summary>
public sealed class BedWorkflowSummary
{
    public Guid WorkflowId { get; set; }

    public Guid? AdmissionId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public BedWorkflowStatus Status { get; set; }

    public BedAgentOutcome? Outcome { get; set; }

    public IReadOnlyList<string> Plan { get; set; } = Array.Empty<string>();

    public IReadOnlyList<BedAgentStep> Steps { get; set; } = Array.Empty<BedAgentStep>();

    public BedSuggestionPatient? Patient { get; set; }

    public SuggestedBed? Best { get; set; }

    public IReadOnlyList<SuggestedBed> Alternatives { get; set; } = Array.Empty<SuggestedBed>();

    public BedSuggestionBlocker? Blocker { get; set; }

    public BedWorkflowValidation Validation { get; set; } = new();

    public BedApproverRole? RequiresApprovalBy { get; set; }

    public int Retries { get; set; }
}
