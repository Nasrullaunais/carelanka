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

    /// <summary>
    /// What the desk typed when they called it off, shown to the patient as
    /// written. It is where staff name a better time to come in.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// False when the patient cancelled it themselves, so the app can say
    /// which of them did it rather than guessing.
    /// </summary>
    [Required]
    public bool CancelledByHospital { get; set; }
}
