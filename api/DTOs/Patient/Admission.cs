using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>One hospital visit, in full. The spec builds this from AdmissionSummary + AuditFields, so this inherits rather than repeating the summary fields.</summary>
public class Admission : AdmissionSummary
{
    /// <summary>Emergency Service's own reference, carried as their string. Not a foreign key — dispatches are their table.</summary>
    public string? DispatchId { get; set; }

    /// <summary>The clinician who chose the care level. Recorded proof a human decided it, never an agent.</summary>
    [Required]
    public Guid CategorySetByStaffId { get; set; }

    /// <summary>
    /// Who admitted this patient and chose their care level. For a walk-in that is whoever was
    /// on the desk, which is the closest thing this component stores to "who did the intake" -
    /// there are no created_by columns anywhere, by group convention.
    /// </summary>
    public string? CategorySetByStaffName { get; set; }

    [Required]
    public DateTimeOffset CategorySetAt { get; set; }

    [Required]
    public bool IsInfectious { get; set; }

    /// <summary>
    /// A PatientAccount id — the app user who raised the emergency call when they are not the
    /// patient. Never a Patient id: a bystander who calls for a stranger has a login, not a
    /// medical record. Null for a walk-in or a booking.
    /// </summary>
    public Guid? ReportedByUserId { get; set; }

    /// <summary>
    /// What paperwork is still outstanding, named rather than counted. "Incomplete" does not
    /// tell a ward clerk what to chase; "nic, date_of_birth" does.
    /// </summary>
    [Required]
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();

    /// <summary>When the patient left. Null while the visit is still running.</summary>
    public DateTimeOffset? DischargedAt { get; set; }

    /// <summary>Why the visit was called off. Null unless the status is `cancelled`.</summary>
    public CancelReason? CancelReason { get; set; }

    /// <summary>
    /// The free-text half of a cancellation, which the enum cannot carry. Null unless the
    /// status is `cancelled`, and often null even then.
    /// </summary>
    public string? CancelNote { get; set; }

    // Not [Required]: created_at and updated_at come from the group-owned AuditFields schema,
    // which lists no required members in any of the five specs.
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
