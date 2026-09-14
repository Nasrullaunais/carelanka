using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;

namespace CareLanka.Api.DTOs.Common;

public class PatientRegisterRequest
{
    [Required]
    [MinLength(UsernameRules.MinLength)]
    [MaxLength(UsernameRules.MaxLength)]
    [RegularExpression(UsernameRules.Pattern,
        ErrorMessage = "Use letters, numbers, dots, underscores or hyphens.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
