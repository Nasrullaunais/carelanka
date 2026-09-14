using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
