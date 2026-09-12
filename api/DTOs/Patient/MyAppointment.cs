using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>What a patient may see about their own booking.</summary>
/// <remarks>
/// A separate shape, not a filtered staff object — same reasoning as <see cref="MyAdmission"/>.
/// It carries neither who took the booking nor which admission it became, and neither is the
/// patient's business.
/// </remarks>
public class MyAppointment
{
    [Required]
    public Guid AppointmentId { get; set; }

    [Required]
    public DateTimeOffset ScheduledAt { get; set; }

    [Required]
    public AppointmentStatus Status { get; set; }

    /// <summary>
    /// The same status in plain language, e.g. "Booked — you can still cancel this".
    /// </summary>
    /// <remarks>
    /// Deliberately carries no date. See <see cref="MyAdmission.StatusText"/>: the server has no
    /// timezone, the phone does, and the app renders <c>scheduled_at</c> itself.
    /// </remarks>
    [Required]
    public string StatusText { get; set; } = null!;

    public string? Reason { get; set; }

    /// <summary>
    /// True only while <c>scheduled</c>. Published so the app hides the cancel button rather
    /// than offering one the server will refuse.
    /// </summary>
    [Required]
    public bool CanCancel { get; set; }
}
