using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class NotificationDelivery : AuditedEntity
{
    public Guid NotificationId { get; set; }

    public Notification Notification { get; set; } = null!;

    public NotificationChannel Channel { get; set; }

    public NotificationStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string? FailureReason { get; set; }
}
