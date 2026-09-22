using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public class DispatchProposalSummary
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid EmergencyCallId { get; set; }
    public CallPriority CallPriority { get; set; }
    public DispatchProposalStatus Status { get; set; }
    public DispatchOutcome? Outcome { get; set; }
    public bool IsDiversion { get; set; }
    public string? ProposedAmbulanceRegistration { get; set; }
    public int? EstimatedMinutesToScene { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
