using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class PatientAppPasswordReset
{
    [Required]
    public string Username { get; set; } = null!;

    [Required]
    public string TemporaryPassword { get; set; } = null!;
}
