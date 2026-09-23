using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>
/// Persisted agent execution state for the reorder-threshold advisor. Same shape and purpose as
/// <c>CareWorkflowSummary</c> - the model's raw reasoning is not stored, only the summary.
/// </summary>
public sealed class ReorderWorkflowSummary
{
    public Guid WorkflowId { get; set; }

    public Guid SuggestionId { get; set; }

    public Guid PharmacyItemId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public ReorderWorkflowStatus Status { get; set; }

    public IReadOnlyList<string> Plan { get; set; } = Array.Empty<string>();

    public IReadOnlyList<CareAgentStep> Steps { get; set; } = Array.Empty<CareAgentStep>();

    public int CurrentThreshold { get; set; }

    public int CurrentQuantityOnHand { get; set; }

    public int? SuggestedThreshold { get; set; }

    /// <summary>A one-sentence reason for the reviewer. Never the model's raw chain of thought.</summary>
    public string? Reasoning { get; set; }

    /// <summary>Whether the number is the model's own reasoning about this medicine or the fixed
    /// formula - null while the run is still going.</summary>
    public ReorderSuggestionSource? Source { get; set; }

    public int Retries { get; set; }
}
