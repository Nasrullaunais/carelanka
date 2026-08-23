using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.Services.Common;

/// <summary>
/// Bound from the "Jwt" section of appsettings. Validated at startup, so a missing or
/// too-short signing key fails the app immediately instead of at the first login.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32, ErrorMessage = "The JWT signing key must be at least 32 characters.")]
    public string Key { get; set; } = string.Empty;

    [Required] public string Issuer { get; set; } = "CareLanka";
    [Required] public string Audience { get; set; } = "CareLanka";

    [Range(1, 1440)] public int AccessTokenMinutes { get; set; } = 60;
    [Range(1, 365)] public int RefreshTokenDays { get; set; } = 14;
}
