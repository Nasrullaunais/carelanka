using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class PatientLookupResult
{
    [Required]
    public bool Found { get; set; }

    public PatientSummary? Patient { get; set; }

    [Required]
    public bool HasOpenAdmission { get; set; }
}
