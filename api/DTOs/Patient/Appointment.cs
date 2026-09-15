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

    public DateTimeOffset? ConfirmedAt { get; set; }

    public Guid? ConfirmedByStaffId { get; set; }

    /// <summary>
    /// Null when the patient was seen and went home, set when they were admitted. The status
    /// reads <c>completed</c> either way, so this is what tells the desk which happened.
    /// </summary>
    public Guid? AdmissionId { get; set; }

    public string? CancellationReason { get; set; }

    public Guid? CancelledByStaffId { get; set; }

    [Required]
    public bool CanConfirm { get; set; }

    [Required]
    public bool CanCancel { get; set; }

    /// <summary>
    /// True for a confirmed booking, and the gate on all three day-of actions: admitting the
    /// patient, recording them as seen, and marking them as never having come.
    /// </summary>
    [Required]
    public bool CanComplete { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
