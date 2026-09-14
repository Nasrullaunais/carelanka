using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class CreateAppointmentRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public DateTimeOffset? ScheduledAt { get; set; }

    [MaxLength(300)]
    public string? Reason { get; set; }
}
