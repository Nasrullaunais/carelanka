namespace CareLanka.Api.DTOs.Staff;

public class PlanStepDto
{
    public int Sequence { get; set; }

    public int Step
    {
        get => Sequence;
        set => Sequence = value;
    }

    public string? AgentRole { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
