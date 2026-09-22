using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCallSummary
{
    public Guid Id { get; set; }
    public CallPriority Priority { get; set; }
    public CallStatus Status { get; set; }
    public string? CallerName { get; set; }
    public string? AddressLabel { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Guid? ActiveDispatchId { get; set; }
    public Guid? OpenProposalId { get; set; }
    public int WaitingMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
