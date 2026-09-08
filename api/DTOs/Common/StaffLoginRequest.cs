using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>Body of POST /api/auth/login.</summary>
public class StaffLoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
