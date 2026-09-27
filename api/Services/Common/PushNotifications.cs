using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

public interface IPushNotifications
{
    void Stage(IEnumerable<Guid> staffMemberIds, string title, string body, string entityType, Guid entityId, string reason);
}

public sealed class PushNotifications(CareLankaDbContext db, TimeProvider clock) : IPushNotifications
{
    public void Stage(IEnumerable<Guid> staffMemberIds, string title, string body, string entityType, Guid entityId, string reason)
    {
        var now = clock.GetUtcNow();
        foreach (var staffMemberId in staffMemberIds.Distinct())
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                RecipientStaffMemberId = staffMemberId,
                Type = NotificationType.DispatchAssigned,
                Title = title,
                Body = body,
                EntityType = entityType,
                EntityId = entityId,
                DedupeKey = $"{reason}:{entityId}:{staffMemberId}"
            };
            db.Notifications.Add(notification);
            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                Notification = notification,
                Channel = NotificationChannel.Push,
                Status = NotificationStatus.Queued,
                NextAttemptAt = now
            });
        }
    }
}
