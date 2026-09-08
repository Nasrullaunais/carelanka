using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>Body of POST /api/auth/refresh and POST /api/auth/logout.</summary>
public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
