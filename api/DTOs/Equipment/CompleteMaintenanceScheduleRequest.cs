using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/maintenance-schedules/{id}/complete. Who did the work comes from the token, never the body.</summary>
public class CompleteMaintenanceScheduleRequest
{
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
