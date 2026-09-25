namespace CareLanka.Api.DTOs.Patient;

public sealed class CareWorkflowAccepted
{
    public Guid WorkflowId { get; set; }

    public Guid RecommendationId { get; set; }

    public string Status { get; set; } = "running";

    public string PollUrl { get; set; } = string.Empty;
}
