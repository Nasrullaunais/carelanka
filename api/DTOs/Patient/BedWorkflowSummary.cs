using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Persisted agent execution state: id, objective, plan, completed steps, tool calls with
/// timings, validation results, errors, retries, approval status and outcome - plus the answer
/// itself. The model's raw internal reasoning is not stored.
/// </summary>
public class BedWorkflowSummary
{
    [Required]
    public Guid WorkflowId { get; set; }

    public Guid? AdmissionId { get; set; }

    [Required]
    public WorkflowObjective Objective { get; set; }

    [Required]
    public AgentWorkflowStatus Status { get; set; }

    public BedAgentOutcome? Outcome { get; set; }

    public List<string> Plan { get; set; } = [];

    public List<BedWorkflowStep> Steps { get; set; } = [];

    public List<BedWorkflowToolCall> ToolCalls { get; set; } = [];

    public List<string> Errors { get; set; } = [];

    [Required]
    public int AttemptCount { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Null only when the identifier matched nobody.</summary>
    public BedSuggestionPatient? Patient { get; set; }

    /// <summary>The agent's top pick. Null on every blocked outcome.</summary>
    public SuggestedBed? Best { get; set; }

    /// <summary>
    /// Every other bed that passed all seven hard rules, ranked below <see cref="Best"/> and each
    /// one selectable with the same button. Empty when there was only one, or none.
    /// </summary>
    public List<SuggestedBed> Alternatives { get; set; } = [];

    public BedSuggestionBlocker? Blocker { get; set; }

    public BedSuggestionValidation? Validation { get; set; }

    public BedApproverRole? RequiresApprovalBy { get; set; }
}

public class BedWorkflowStep
{
    [Required]
    public string Step { get; set; } = null!;

    /// <summary>The allow-listed tool invoked, if any. Null for a planning or decision step.</summary>
    public string? Tool { get; set; }

    [Required]
    public DateTimeOffset StartedAt { get; set; }

    [Required]
    public int DurationMs { get; set; }

    [Required]
    public bool Ok { get; set; }

    public string? Error { get; set; }
}

/// <summary>
/// One allow-listed tool call with its inputs, a summary of its output and its timing. The
/// output is a summary - counts and ids - never the whole payload.
/// </summary>
public class BedWorkflowToolCall
{
    [Required]
    public string Tool { get; set; } = null!;

    [Required]
    public string Input { get; set; } = null!;

    [Required]
    public string Output { get; set; } = null!;

    [Required]
    public DateTimeOffset StartedAt { get; set; }

    [Required]
    public int DurationMs { get; set; }

    [Required]
    public bool Ok { get; set; }

    public string? Error { get; set; }
}

/// <summary>
/// Result of the deterministic C# validator, run over the best pick and every alternative before
/// any of them reach a human. Anything failing here is removed from the answer. It runs a second
/// time, under a row lock, at <c>POST /admissions/{id}/assign-bed</c>.
/// </summary>
public class BedSuggestionValidation
{
    [Required]
    public bool Passed { get; set; }

    public List<string> FailedRules { get; set; } = [];
}
