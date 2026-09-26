namespace CareLanka.Api.DTOs.Staff;

public class RosterProposalErrorDto
{
    public string Step { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}
