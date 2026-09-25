using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class AgentWorkflow : AuditedEntity
{
    public AgentType AgentType { get; set; }

    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? ParentWorkflowId { get; set; }

    public AgentWorkflow? ParentWorkflow { get; set; }

    public string Objective { get; set; } = null!;

    public string Plan { get; set; } = "[]";

    public string CompletedSteps { get; set; } = "[]";

    public string ToolResults { get; set; } = "[]";

    public string? ValidationResults { get; set; }

    public string? Errors { get; set; }

    public AgentWorkflowStatus Status { get; set; }

    public StaffRole? RequiredApproverRole { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int AttemptCount { get; set; }

    public Guid? ReviewedByStaffMemberId { get; set; }

    public StaffMember? ReviewedByStaffMember { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    public string? FinalOutcome { get; set; }

    public ICollection<AgentProposedChange> ProposedChanges { get; set; } = new List<AgentProposedChange>();
}
