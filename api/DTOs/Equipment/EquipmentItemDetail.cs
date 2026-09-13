using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class EquipmentItemDetail : EquipmentItem
{
    [Required]
    public IReadOnlyList<MaintenanceSchedule> MaintenanceHistory { get; set; }
        = Array.Empty<MaintenanceSchedule>();

    [Required]
    public IReadOnlyList<Warning> OpenWarnings { get; set; } = Array.Empty<Warning>();
}
