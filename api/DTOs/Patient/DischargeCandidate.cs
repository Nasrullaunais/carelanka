using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// A patient who could go home. Produced by a plain rule - every mandatory box ticked - and not
/// by the agent: checking whether three boxes are ticked is a WHERE clause.
/// </summary>
public class DischargeCandidate
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    /// <summary>Empty when the patient holds no bed.</summary>
    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public AdmissionCategory AdmissionCategory { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }

    [Required]
    public int DaysInBed { get; set; }

    /// <summary>Empty for a true candidate. Populated rows are shown as "nearly ready".</summary>
    [Required]
    public IReadOnlyList<string> OutstandingItems { get; set; } = Array.Empty<string>();

    /// <summary>
    /// True when this patient has already gone home. Only ever set on rows returned because
    /// `includeDischarged` was asked for.
    /// </summary>
    /// <remarks>
    /// A finished visit is not a candidate for anything, and the screen must not offer a
    /// control on one. It is here because the discharge screen is also where somebody goes to
    /// look up a discharge that has already happened, and a list that forgets every patient the
    /// moment they leave is a list with no record in it.
    /// </remarks>
    [Required]
    public bool IsDischarged { get; set; }

    /// <summary>When they actually left. Null while they are still in the building.</summary>
    public DateTimeOffset? DischargedAt { get; set; }
}
