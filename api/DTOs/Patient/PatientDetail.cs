using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>One patient with every visit they have ever had. One patient, many admissions — never a second row for a returning person.</summary>
public class PatientDetail : Patient
{
    /// <summary>Every visit, newest first. Empty for someone registered but not yet admitted — an empty list, never absent.</summary>
    [Required]
    public IReadOnlyList<AdmissionSummary> Admissions { get; set; } = Array.Empty<AdmissionSummary>();
}
