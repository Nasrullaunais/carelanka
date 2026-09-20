using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class AgentProposedChange : AuditedEntity
{
    public Guid AgentWorkflowId { get; set; }

    public AgentWorkflow AgentWorkflow { get; set; } = null!;

    public int Sequence { get; set; }

    public ProposedChangeType ChangeType { get; set; }

    public string? TargetEntityType { get; set; }

    public Guid? TargetEntityId { get; set; }

    public Guid? ProposedStaffMemberId { get; set; }

    public Guid? ProposedBedId { get; set; }

    public Guid? ProposedWardId { get; set; }

    public string Payload { get; set; } = "{}";

    public ProposedChangeValidationStatus ValidationStatus { get; set; }

    public string? ValidationMessage { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public Guid? AppliedEntityId { get; set; }
}
