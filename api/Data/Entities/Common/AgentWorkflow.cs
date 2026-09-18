using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// One row per agent run. Group-owned (ADR 3) and shared by all five agents, linked to whatever
/// domain row the run is about through the polymorphic <see cref="EntityType"/> /
/// <see cref="EntityId"/> pair rather than a foreign key per component.
/// </summary>
/// <remarks>
/// STUB - see STUBS.md. This is common, not Patient Management's, and it is here only because the
/// bed agent cannot persist its run without it. The shape is copied from
/// <c>docs/entity_diagram.md</c> and <c>specs/common-spec.yaml</c> so the group's own version can
/// replace it without this component moving. <c>AgentProposedChange</c> is deliberately NOT here:
/// the bed agent holds no write tool, so it produces no proposal rows and there was nothing to
/// copy honestly.
/// </remarks>
public class AgentWorkflow : AuditedEntity
{
    public AgentType AgentType { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Shared by every run in one coordinated plan, so the chain is one query.</summary>
    public Guid CorrelationId { get; set; }

    public Guid? ParentWorkflowId { get; set; }

    public WorkflowObjective Objective { get; set; }

    /// <summary>The ordered plan, written before the first step runs.</summary>
    public List<string> Plan { get; set; } = [];

    public List<WorkflowStepRecord> CompletedSteps { get; set; } = [];

    public List<ToolCallRecord> ToolResults { get; set; } = [];

    public List<WorkflowValidationRecord> ValidationResults { get; set; } = [];

    public List<string> Errors { get; set; } = [];

    public AgentWorkflowStatus Status { get; set; }

    /// <summary>
    /// Persisted rather than derived, so the authorization rule is visible in the audit trail
    /// instead of buried in C#.
    /// </summary>
    public StaffRole? RequiredApproverRole { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int AttemptCount { get; set; }

    public Guid? ReviewedByStaffMemberId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    public string? FinalOutcome { get; set; }
}

/// <summary>
/// One executed step. <see cref="Tool"/> is null for a planning or decision step that called
/// nothing.
/// </summary>
public sealed record WorkflowStepRecord(
    string Step,
    string? Tool,
    DateTimeOffset StartedAt,
    int DurationMs,
    bool Ok,
    string? Error);

/// <summary>
/// One allow-listed tool call, with its inputs, a summary of its output and its timing -
/// the three things assignment §9.1 names under Observability.
/// </summary>
/// <remarks>
/// <paramref name="Output"/> is a summary (counts, ids), never the whole payload and never the
/// model's reasoning. §6 forbids persisting hidden reasoning.
/// </remarks>
public sealed record ToolCallRecord(
    string Tool,
    string Input,
    string Output,
    DateTimeOffset StartedAt,
    int DurationMs,
    bool Ok,
    string? Error);

/// <summary>The verdict from one deterministic check - ordinary C#, never the model marking itself.</summary>
public sealed record WorkflowValidationRecord(
    string Rule,
    bool Passed,
    string? Message,
    DateTimeOffset CheckedAt);
