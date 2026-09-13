using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class PatientDetail : Patient
{
    [Required]
    public IReadOnlyList<AdmissionSummary> Admissions { get; set; } = Array.Empty<AdmissionSummary>();
}
