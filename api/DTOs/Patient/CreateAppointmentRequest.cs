using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of POST /api/appointments — the desk booking a visit for someone who phoned in or
/// walked up without the app.
/// </summary>
/// <remarks>
/// There is no <c>booked_by_staff_id</c> on here on purpose. Who took the booking comes off
/// the token, never off the body: a field a client fills in is a field a client can fill in
/// with somebody else's name, and this one is the only thing separating a desk booking from
/// a self-booking afterwards.
/// </remarks>
public class CreateAppointmentRequest
{
    [Required]
    public Guid PatientId { get; set; }

    /// <summary>
    /// When the patient intends to come in. Must be in the future — booking a visit for a
    /// time that has passed is always a typo, and somebody already here is admitted, not
    /// booked.
    /// </summary>
    /// <remarks>
    /// Nullable for the same reason the enums on the other request bodies are: <c>[Required]</c>
    /// on a plain value type always passes, because the model binder has already turned an
    /// absent key into <c>default</c>. Left non-nullable, an omitted <c>scheduled_at</c> would
    /// book the visit for the first of January in the year 1.
    /// </remarks>
    [Required]
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>Optional free text, for the desk to read. Never read by the agent.</summary>
    [MaxLength(300)]
    public string? Reason { get; set; }
}
