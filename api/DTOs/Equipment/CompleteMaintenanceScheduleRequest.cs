using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class CompleteMaintenanceScheduleRequest
{
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
