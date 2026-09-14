using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCancellationRequest
{
    public Guid EmergencyCallId { get; set; }
    public CancellationRequestStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}
