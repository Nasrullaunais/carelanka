namespace CareLanka.Api.DTOs.Common;

/// <summary>What GET /api/health reports.</summary>
public class HealthStatus
{
    /// <summary><c>healthy</c> or <c>degraded</c>.</summary>
    public string Status { get; set; } = "healthy";

    /// <summary><c>up</c> or <c>down</c>.</summary>
    public string Database { get; set; } = "up";

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset CheckedAt { get; set; }
}
