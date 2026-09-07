namespace CareLanka.Api.DTOs.Common;

/// <summary>What every successful sign-in returns.</summary>
public class AuthTokens
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    /// <summary>Access token lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Single-use — the next refresh replaces it. Store it in secure storage
    /// (<c>flutter_secure_storage</c> on mobile), never in plain preferences or
    /// <c>localStorage</c>.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    public CurrentPrincipal Principal { get; set; } = null!;
}
