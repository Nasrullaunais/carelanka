using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class RosterProposalDetail : RosterProposalSummary
{
    public IReadOnlyList<PlanStepDto> Plan { get; set; } = [];

    public IReadOnlyList<RosterProposedChangeDto> ProposedChanges { get; set; } = [];

    public IReadOnlyList<RosterValidationResult> Validation { get; set; } = [];

    public IReadOnlyList<ToolCallDto> ToolCalls { get; set; } = [];

    public IReadOnlyList<RosterProposalErrorDto> Errors { get; set; } = [];

    public int AttemptCount { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? ReviewedByStaffId { get; set; }

    public Guid? ReviewedByStaffMemberId
    {
        get => ReviewedByStaffId;
        set => ReviewedByStaffId = value;
    }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    public RejectionReason? RejectionReason { get; set; }

    public string? FinalOutcome { get; set; }
}
