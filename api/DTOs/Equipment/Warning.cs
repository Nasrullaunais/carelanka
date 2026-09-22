using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

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

    /// <summary>The medicine, machine or bed by name, so a list does not have to look each one up.</summary>
    public string? RelatedEntityLabel { get; set; }

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
