namespace CareLanka.Api.DTOs.Common;

public class HealthStatus
{
    public string Status { get; set; } = "healthy";

    public string Database { get; set; } = "up";

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset CheckedAt { get; set; }
}
