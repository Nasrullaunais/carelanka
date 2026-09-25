namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Persisted agent execution state for the care advisory agent. The model's raw reasoning is not
/// stored, only the summary.
/// </summary>
public sealed class CareWorkflowSummary
{
    public Guid WorkflowId { get; set; }

    public Guid? RecommendationId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public CareWorkflowStatus Status { get; set; }

    public CareAgentOutcome? Outcome { get; set; }

    public IReadOnlyList<string> Plan { get; set; } = Array.Empty<string>();

    public IReadOnlyList<CareAgentStep> Steps { get; set; } = Array.Empty<CareAgentStep>();

    /// <summary>
    /// Result of the deterministic keyword screen, run before the model.
    /// </summary>
    public bool RedFlag { get; set; }

    public CareWorkflowValidation Validation { get; set; } = new();

    /// <summary>
    /// Whether the draft is the model's own note about this patient or the fixed backup sentence.
    /// </summary>
    public CareDraftSource DraftSource { get; set; }

    /// <summary>
    /// Plain-language reason the backup note was used, for the reviewer. Null on a model draft.
    /// </summary>
    public string? DraftNote { get; set; }

    public int Retries { get; set; }
}
