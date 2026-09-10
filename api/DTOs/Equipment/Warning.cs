using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>A problem the threshold sweep found, or a fault a person reported.</summary>
public class Warning
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public WarningType Type { get; set; }

    [Required]
    public WarningSeverity Severity { get; set; }

    [Required]
    public RelatedEntityType RelatedEntityType { get; set; }

    [Required]
    public Guid RelatedEntityId { get; set; }

    public Guid? WardId { get; set; }

    [Required]
    public string RecommendedAction { get; set; } = string.Empty;

    [Required]
    public WarningStatus Status { get; set; }

    [Required]
    public RaisedBy RaisedBy { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? AcknowledgedByStaffId { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
