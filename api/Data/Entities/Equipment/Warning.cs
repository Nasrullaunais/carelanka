using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

public class Warning : AuditedEntity
{
    public WarningType Type { get; set; }

    public WarningSeverity Severity { get; set; }

    public RelatedEntityType RelatedEntityType { get; set; }

    public Guid RelatedEntityId { get; set; }

    public Guid? WardId { get; set; }

    public string RecommendedAction { get; set; } = null!;

    public WarningStatus Status { get; set; }

    public RaisedBy RaisedBy { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? AcknowledgedByStaffId { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>When the administrator marked a resolved warning done, taking it off the list.
    /// The row stays in the database for the record.</summary>
    public DateTimeOffset? ClearedAt { get; set; }

    public Guid? ClearedByStaffId { get; set; }
}
