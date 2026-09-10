using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>An equipment item with everything servicing and monitoring know about it.</summary>
public class EquipmentItemDetail : EquipmentItem
{
    [Required]
    public IReadOnlyList<MaintenanceSchedule> MaintenanceHistory { get; set; }
        = Array.Empty<MaintenanceSchedule>();

    /// <summary>Only warnings still open. Acknowledged and dismissed ones are history, not a to-do list.</summary>
    [Required]
    public IReadOnlyList<Warning> OpenWarnings { get; set; } = Array.Empty<Warning>();
}
