using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.Common.Auth;

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

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 30;
}
