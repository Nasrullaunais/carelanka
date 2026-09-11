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
}
