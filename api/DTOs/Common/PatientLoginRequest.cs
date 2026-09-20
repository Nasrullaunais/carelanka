using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;

namespace CareLanka.Api.DTOs.Common;

public class PatientLoginRequest
{
    [Required]
    [MaxLength(UsernameRules.MaxLength)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
