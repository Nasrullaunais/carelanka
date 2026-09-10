using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// A problem the monitoring sweep found, or a fault a person reported. Advisory until acted
// on: raising one never changes stock, status or a schedule by itself.
//
// Deliberately not agent-generated. The thresholds are ordinary code, and the agent reviews
// what they produce rather than deciding what counts as a problem.
public class Warning : AuditedEntity
{
    public WarningType Type { get; set; }

    public WarningSeverity Severity { get; set; }

    // Polymorphic, like MaintenanceSchedule: one warning queue rather than one per subject.
    public RelatedEntityType RelatedEntityType { get; set; }

    public Guid RelatedEntityId { get; set; }

    public Guid? WardId { get; set; }

    // A short human sentence, never the model's raw reasoning.
    public string RecommendedAction { get; set; } = null!;

    public WarningStatus Status { get; set; }

    public RaisedBy RaisedBy { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? AcknowledgedByStaffId { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
