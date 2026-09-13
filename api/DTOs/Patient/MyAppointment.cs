using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class MyAppointment
{
    [Required]
    public Guid AppointmentId { get; set; }

    [Required]
    public DateTimeOffset ScheduledAt { get; set; }

    [Required]
    public AppointmentStatus Status { get; set; }

    [Required]
    public string StatusText { get; set; } = null!;

    public string? Reason { get; set; }

    [Required]
    public bool CanCancel { get; set; }
}
