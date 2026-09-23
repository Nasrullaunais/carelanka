namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchValidationResult
{
    public string Check { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; }
}
