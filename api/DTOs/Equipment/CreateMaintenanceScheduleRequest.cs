using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/maintenance-schedules. The manual path, with no agent involved.</summary>
public class CreateMaintenanceScheduleRequest
{
    [Required]
    public AssetType AssetType { get; set; }

    /// <summary>The equipment item or bed being serviced. Polymorphic, so it carries no foreign key.</summary>
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public MaintenanceType ScheduleType { get; set; }

    [Required]
    public DateOnly ScheduledDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
