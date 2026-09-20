using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Common;

public sealed class RegisterDeviceRequest
{
    public string Token { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }
}
