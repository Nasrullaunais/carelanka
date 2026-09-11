using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One servicing event against an equipment item or a bed.</summary>
public class MaintenanceSchedule
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public AssetType AssetType { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    /// <summary>Human-readable, so a task list does not read as a column of GUIDs. Built from whichever asset the row points at.</summary>
    [Required]
    public string AssetLabel { get; set; } = string.Empty;

    [Required]
    public MaintenanceType ScheduleType { get; set; }

    [Required]
    public DateOnly ScheduledDate { get; set; }

    /// <summary>Overdue is computed here at read time, never stored.</summary>
    [Required]
    public MaintenanceStatus Status { get; set; }

    public Guid? PerformedByStaffId { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Notes { get; set; }

    [Required]
    public RaisedBy CreatedBy { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
