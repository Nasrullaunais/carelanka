using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>What every successful sign-in returns.</summary>
public class AuthTokens
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Access token lifetime in seconds.</summary>
    [Required]
    public int ExpiresIn { get; set; }

    /// <summary>Single-use — the next refresh replaces it. Keep it in secure storage, never in localStorage or plain preferences.</summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    [Required]
    public CurrentPrincipal Principal { get; set; } = null!;
}
