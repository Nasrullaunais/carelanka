using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>Body of POST /api/auth/patient/login.</summary>
public class PatientLoginRequest
{
    /// <summary>The login identifier for a patient account, e.g. <c>+94771234567</c>.</summary>
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
