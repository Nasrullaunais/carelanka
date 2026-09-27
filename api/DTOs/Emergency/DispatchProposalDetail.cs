using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchProposalDetail : DispatchProposalSummary
{
    public string Objective { get; set; } = string.Empty;
    public Guid? ProposedAmbulanceId { get; set; }
    public int? ProposedAmbulanceCurrentCrewCount { get; set; }
    public int? ProposedAmbulanceRequiredCrewCount { get; set; }
    public string? Rationale { get; set; }
    public DiversionImpact? DiversionImpact { get; set; }
    public IReadOnlyList<DispatchPlanStep> Plan { get; set; } = [];
    public IReadOnlyList<DispatchValidationResult> Validation { get; set; } = [];
    public IReadOnlyList<DispatchToolCall> ToolCalls { get; set; } = [];
    public IReadOnlyList<DispatchProposalError> Errors { get; set; } = [];
    public int AttemptCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? ResultingDispatchId { get; set; }
    public Guid? ReviewedByStaffMemberId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DispatchRejectionReason? RejectionReason { get; set; }
}

public sealed class DispatchProposalError
{
    public string Step { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
