namespace CareLanka.Api.DTOs.Staff;

public class ToolCallDto
{
    public string ToolName { get; set; } = string.Empty;

    public string Tool
    {
        get => ToolName;
        set => ToolName = value;
    }

    public IReadOnlyDictionary<string, object?> Arguments { get; set; } =
        new Dictionary<string, object?>();

    public bool Succeeded { get; set; }

    public int DurationMs { get; set; }

    public string? Error { get; set; }

    public string? Summary { get; set; }

    public DateTimeOffset CalledAt { get; set; }
}
