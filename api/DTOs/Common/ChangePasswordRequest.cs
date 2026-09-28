using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Common;

public sealed class ChangePasswordRequest
{
    [JsonRequired]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonRequired]
    public string NewPassword { get; set; } = string.Empty;
}
