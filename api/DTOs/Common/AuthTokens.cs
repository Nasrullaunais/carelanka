using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

public class AuthTokens
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string TokenType { get; set; } = "Bearer";

    [Required]
    public int ExpiresIn { get; set; }

    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    [Required]
    public CurrentPrincipal Principal { get; set; } = null!;
}
