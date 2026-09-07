using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.Common.Auth;

/// <summary>
/// Bound from configuration section <c>Jwt</c>.
/// <para>
/// <strong><see cref="SigningKey"/> is never in <c>appsettings.json</c> and never in
/// Git.</strong> Locally it comes from <c>dotnet user-secrets</c>; in deployment from an
/// environment variable. Startup fails loudly if it is missing, rather than quietly
/// signing tokens with a default everyone can guess.
/// </para>
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false, ErrorMessage =
        "Jwt:SigningKey is not configured. Run: dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 characters>\" --project api")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters — HMAC-SHA256 needs a 256-bit key.")]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "carelanka-api";

    [Required]
    public string Audience { get; set; } = "carelanka-clients";

    /// <summary>
    /// Short on purpose. Revoking a session is the refresh token's job, not the access
    /// token's — a stateless token cannot be called back once issued.
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 30;
}
