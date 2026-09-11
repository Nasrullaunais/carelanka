using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>A booked visit, as staff see it. The patient's own view of the same booking is MyAppointment.</summary>
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

    /// <summary>
    /// Free text, written by whoever booked. Displayed to staff and never read by the bed
    /// agent — free text stays data, never instructions.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Who took the booking. Null for a self-booking from the patient app, which is how the
    /// two paths stay tellable apart afterwards.
    /// </summary>
    public Guid? BookedByStaffId { get; set; }

    /// <summary>Set once checked in. Until then there is no admission to point at.</summary>
    public Guid? AdmissionId { get; set; }

    // Not [Required]: created_at and updated_at come from the group-owned AuditFields schema,
    // which lists no required members in any of the five specs.
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
