namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One executed step of the run. Named per component rather than <c>AgentStep</c> because schema
/// names are global across all four specs.
/// </summary>
public sealed class BedAgentStep
{
    public string Step { get; set; } = string.Empty;

    public string? Tool { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public int DurationMs { get; set; }

    public bool Ok { get; set; }

    public string? Error { get; set; }
}
