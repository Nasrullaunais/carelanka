using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCancellationRequest
{
    public Guid EmergencyCallId { get; set; }
    public CallPriority CallPriority { get; set; }
    public CallStatus CallStatus { get; set; }
    public string? CallerName { get; set; }
    public string? AddressLabel { get; set; }
    public DateTimeOffset CallCreatedAt { get; set; }
    public string? ActiveAmbulanceRegistration { get; set; }
    public CancellationRequestStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByStaffId { get; set; }
    public string? ReviewNotes { get; set; }
}
