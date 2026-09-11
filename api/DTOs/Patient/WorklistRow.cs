using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One line of the ward board: one patient, and what is happening with them right now.
/// </summary>
/// <remarks>
/// Two tables feed this, because the question a nurse asks does not respect the table
/// boundary. "Who are we dealing with today" includes the woman booked in for a scan at
/// eleven who has not walked in yet, and she is an <c>Appointment</c>; it also includes the
/// man in bed 4, and he is an <c>Admission</c>. A screen that showed only admissions could not
/// say "not arrived" about anybody, because an admission is created by arriving.
///
/// A booking that has been checked in appears **once**, as its visit. The appointment row is
/// terminal at <c>checked_in</c> and is left out, so nobody is counted twice.
///
/// Deliberately not <c>AdmissionSummary</c> with extra nullable fields. Half the columns on a
/// booking are genuinely unknown rather than null-as-in-empty - nobody has set a care level
/// for a patient who has not arrived - and making the admission schema pretend otherwise
/// would make every one of its fields optional for every caller.
/// </remarks>
public class WorklistRow
{
    /// <summary>The appointment id or the admission id, depending on <c>kind</c>.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>Which of the two this is, and therefore which endpoints apply to it.</summary>
    [Required]
    public WorklistKind Kind { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    /// <summary>The board's answer, in the words a nurse uses. Derived; see WorklistStatus.</summary>
    [Required]
    public WorklistStatus Status { get; set; }

    /// <summary>
    /// Whether this visit needs a bed at all. False for an outpatient scan or blood test, and
    /// false for a booking, because nobody has chosen a care level for it yet.
    /// </summary>
    [Required]
    public bool RequiresBed { get; set; }

    /// <summary>How they came in. Null on a booking - they have not come in.</summary>
    public AdmissionSource? Source { get; set; }

    /// <summary>Null on a booking. The care level is set by staff at check-in, never at booking time.</summary>
    public AdmissionCategory? AdmissionCategory { get; set; }

    public AdmissionUrgency? Urgency { get; set; }

    /// <summary>Where they are, from the live bed assignment. Null when no bed is held.</summary>
    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    /// <summary>
    /// The one time that matters for this row: when a booking is due, or when a visit started.
    /// </summary>
    /// <remarks>
    /// Two different facts under one name on purpose. The board is read top to bottom as a
    /// chronology, and a column that was <c>scheduled_at</c> on some rows and blank on others
    /// could not be read that way at all. A visit that has not been marked as arrived falls
    /// back to when its record was opened, which is the closest true thing.
    /// </remarks>
    [Required]
    public DateTimeOffset When { get; set; }

    /// <summary>
    /// Why they are coming, as the desk or the patient typed it - "Scan", "Blood test". Null on
    /// a visit: the reason is not carried onto the admission.
    /// </summary>
    public string? Reason { get; set; }
}
