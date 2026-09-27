namespace CareLanka.Api.DTOs.Equipment;

public sealed class ReorderSuggestionAccepted
{
    public Guid WorkflowId { get; set; }

    public Guid SuggestionId { get; set; }

    public string Status { get; set; } = "running";

    public string PollUrl { get; set; } = string.Empty;
}
