using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Emergency;

public class PreAdmissionNotice : AuditedEntity
{
    public Guid EmergencyCallId { get; set; }
    public EmergencyCall EmergencyCall { get; set; } = null!;
    public Guid DispatchId { get; set; }
    public PreAdmissionStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? FailureReason { get; set; }
}
