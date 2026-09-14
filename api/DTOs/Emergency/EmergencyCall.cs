using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public class EmergencyCall
{
    public Guid Id { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? CallerUserId { get; set; }
    public bool PatientIsCaller { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal LocationAccuracyMetres { get; set; }
    public DateTimeOffset LocationCapturedAt { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string? AddressLabel { get; set; }
    public string? Details { get; set; }
    public CallPriority Priority { get; set; }
    public CallStatus Status { get; set; }
    public string? Outcome { get; set; }
    public bool? Transported { get; set; }
    public CancellationRequestStatus? CancellationRequestStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
