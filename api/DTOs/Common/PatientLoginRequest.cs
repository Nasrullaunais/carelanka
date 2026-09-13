using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

public class PatientLoginRequest
{
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
