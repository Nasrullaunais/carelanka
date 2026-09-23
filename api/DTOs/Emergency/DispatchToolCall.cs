namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Arguments { get; set; } =
        new Dictionary<string, object?>();
    public bool Succeeded { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CalledAt { get; set; }
}
