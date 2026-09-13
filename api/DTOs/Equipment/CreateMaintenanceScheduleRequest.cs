using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class CreateMaintenanceScheduleRequest
{
    [Required]
    public AssetType AssetType { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public MaintenanceType ScheduleType { get; set; }

    [Required]
    public DateOnly ScheduledDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
