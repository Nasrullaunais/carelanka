using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class RosterProposalSummary
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid ShiftId { get; set; }

    public string WardName { get; set; } = string.Empty;

    public DateOnly ShiftDate { get; set; }

    public string Objective { get; set; } = string.Empty;

    public RosterProposalStatus Status { get; set; }

    public AgentOutcome? Outcome { get; set; }

    public bool IsCascadingSwap { get; set; }

    public int ChangeCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
