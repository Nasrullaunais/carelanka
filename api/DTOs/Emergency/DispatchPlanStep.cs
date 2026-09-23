namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchPlanStep
{
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
