using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of POST /api/me/appointments — the patient booking their own visit from the app.
/// </summary>
/// <remarks>
/// The self-service half of <see cref="CreateAppointmentRequest"/>, and it carries no
/// <c>patient_id</c>: the record comes off the token. A route that takes no id cannot have the
/// "forgot to check it against the JWT" bug at all.
///
/// Deliberately narrow: no doctor calendars, no time slots, no availability search and no
/// rescheduling. The patient states when they intend to arrive and the desk sees them on the
/// worklist. Rescheduling is cancel and rebook.
/// </remarks>
public class BookAppointmentRequest
{
    /// <summary>When you intend to come in. Must be in the future.</summary>
    /// <remarks>
    /// Nullable for the reason given on <see cref="CreateAppointmentRequest.ScheduledAt"/>:
    /// left non-nullable, an omitted key books the visit for the year 1 instead of failing.
    /// </remarks>
    [Required]
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>Why you are coming in, in your own words. For the desk to read; never read by an agent.</summary>
    [MaxLength(300)]
    public string? Reason { get; set; }
}
