using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One visit, in list form. Appears on a patient's history and, later, on the admissions list.
/// </summary>
public class AdmissionSummary
{
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Null when this summary is already nested under the patient it belongs to — repeating the
    /// same six fields on every row of one patient's own history says nothing.
    /// </summary>
    public PatientSummary? Patient { get; set; }

    [Required]
    public AdmissionSource Source { get; set; }

    [Required]
    public AdmissionCategory AdmissionCategory { get; set; }

    [Required]
    public AdmissionUrgency Urgency { get; set; }

    [Required]
    public AdmissionStatus Status { get; set; }

    [Required]
    public bool DetailsComplete { get; set; }

    /// <summary>
    /// Whether this visit needs a bed at all. False for an <c>outpatient</c> — a scan or a
    /// blood test is seen and sent home.
    /// </summary>
    /// <remarks>
    /// Derived from <c>admission_category</c> by <c>BedPlacementRules.RequiresBed</c> and
    /// published so a client does not have to re-derive it. A screen that works it out for
    /// itself is a second copy of the rule, and the two drift the first time the categories
    /// change.
    /// </remarks>
    [Required]
    public bool RequiresBed { get; set; }

    /// <summary>From the live bed assignment, if there is one. Null before a bed is held and after discharge.</summary>
    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }
}
