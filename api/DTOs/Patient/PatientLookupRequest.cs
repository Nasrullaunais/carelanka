using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class PatientLookupRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(20)]
    public string Nic { get; set; } = string.Empty;
}
