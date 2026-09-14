using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Emergency;

public class EmergencyCall : AuditedEntity
{
    public Guid? PatientId { get; set; }
    public Guid? CallerUserId { get; set; }
    public bool PatientIsCaller { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal LocationAccuracyMetres { get; set; }
    public DateTimeOffset LocationCapturedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid IdempotencyKey { get; set; } = Guid.NewGuid();
    public string? AddressLabel { get; set; }
    public string? Details { get; set; }
    public CallPriority Priority { get; set; }
    public CallStatus Status { get; set; }
    public string? Outcome { get; set; }
    public bool? Transported { get; set; }
    public CancellationRequestStatus? CancellationRequestStatus { get; set; }
    public string? CancellationRequestReason { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public ICollection<Dispatch> Dispatches { get; set; } = new List<Dispatch>();
}
