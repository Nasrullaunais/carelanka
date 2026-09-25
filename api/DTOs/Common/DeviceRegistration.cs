using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Common;

public sealed class DeviceRegistration
{
    public Guid Id { get; set; }

    public DevicePlatform Platform { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
