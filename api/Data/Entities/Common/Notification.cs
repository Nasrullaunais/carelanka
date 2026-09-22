using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class Notification : AuditedEntity
{
    public Guid RecipientStaffMemberId { get; set; }

    public StaffMember RecipientStaffMember { get; set; } = null!;

    public NotificationChannel Channel { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public NotificationStatus Status { get; set; }

    public string DedupeKey { get; set; } = null!;

    public int AttemptCount { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public string? FailureReason { get; set; }
}
