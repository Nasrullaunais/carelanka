using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public class DispatchSummary
{
    public Guid Id { get; set; }
    public Guid EmergencyCallId { get; set; }
    public string AmbulanceRegistration { get; set; } = string.Empty;
    public CallPriority CallPriority { get; set; }
    public DispatchStatus Status { get; set; }
    public string? DestinationWardName { get; set; }
    public int CrewCount { get; set; }
    public bool AcknowledgementOverdue { get; set; }
    public DateTimeOffset DispatchedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
