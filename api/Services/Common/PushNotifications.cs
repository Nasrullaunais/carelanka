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
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                RecipientStaffMemberId = staffMemberId,
                Channel = NotificationChannel.Push,
                Title = title,
                Body = body,
                EntityType = entityType,
                EntityId = entityId,
                Status = NotificationStatus.Queued,
                DedupeKey = $"{reason}:{entityId}:{staffMemberId}",
                NextAttemptAt = now
            });
        }
    }
}
