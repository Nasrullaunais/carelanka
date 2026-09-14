using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class MyEmergencyCallSummary
{
    public Guid Id { get; set; }
    public bool PatientIsCaller { get; set; }
    public CallPriority Priority { get; set; }
    public CallStatus Status { get; set; }
    public CancellationRequestStatus? CancellationRequestStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
