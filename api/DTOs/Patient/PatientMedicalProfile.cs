using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Staff-facing only. There is no patient-facing equivalent and no /me/ route - showing somebody
/// their own clinical record raises correction rights and wording questions that are a feature in
/// their own right. Every field but the patient id is nullable because an empty profile is the
/// ordinary state of a patient nobody has got to yet, not an error.
/// </summary>
public class PatientMedicalProfile
{
    [Required]
    public Guid PatientId { get; set; }

    public string? KnownConditions { get; set; }

    public string? Allergies { get; set; }

    public string? CurrentSymptoms { get; set; }

    public Guid? UpdatedByStaffId { get; set; }

    public string? UpdatedByStaffName { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
