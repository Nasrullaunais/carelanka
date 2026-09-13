using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class Appointment
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public DateTimeOffset ScheduledAt { get; set; }

    [Required]
    public AppointmentStatus Status { get; set; }

    public string? Reason { get; set; }

    public Guid? BookedByStaffId { get; set; }

    public Guid? AdmissionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
