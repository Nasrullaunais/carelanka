using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class Notification : AuditedEntity
{
    public Guid? RecipientStaffMemberId { get; set; }

    public StaffMember? RecipientStaffMember { get; set; }

    public Guid? RecipientPatientAccountId { get; set; }

    public PatientAccount? RecipientPatientAccount { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string DedupeKey { get; set; } = null!;

    public DateTimeOffset? ReadAt { get; set; }

    public List<NotificationDelivery> Deliveries { get; set; } = [];
}
