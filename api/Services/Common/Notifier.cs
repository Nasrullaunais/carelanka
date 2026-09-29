using CareLanka.Api.Common.Notifications;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Common;

public sealed class Notifier(
    CareLankaDbContext db,
    IRecipientResolver resolver,
    ICurrentUser currentUser,
    TimeProvider clock) : INotifier
{
    public async Task NotifyAsync(
        NotificationType type,
        Recipients to,
        NotificationSubject subject,
        CancellationToken cancellationToken = default,
        params object[] args)
    {
        var recipients = await resolver.ResolveAsync(to, cancellationToken);
        if (recipients.Count == 0) return;

        var (actorStaffId, actorPatientAccountId) = CurrentActor();
        var title = NotificationTexts.Title(type, args);
        var body = NotificationTexts.Body(type, args);
        var now = clock.GetUtcNow();

        foreach (var recipient in recipients)
        {
            if (recipient.StaffMemberId is { } staffId && staffId == actorStaffId) continue;
            if (recipient.PatientAccountId is { } patientAccountId && patientAccountId == actorPatientAccountId) continue;

            var dedupeKey = DedupeKey(type, subject, recipient);
            var alreadyStaged = db.Notifications.Local.Any(n => n.DedupeKey == dedupeKey)
                || await db.Notifications.AnyAsync(n => n.DedupeKey == dedupeKey, cancellationToken);
            if (alreadyStaged) continue;

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                RecipientStaffMemberId = recipient.StaffMemberId,
                RecipientPatientAccountId = recipient.PatientAccountId,
                Type = type,
                Title = title,
                Body = body,
                EntityType = subject.EntityType,
                EntityId = subject.EntityId,
                DedupeKey = dedupeKey
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

    private (Guid? StaffId, Guid? PatientAccountId) CurrentActor()
    {
        if (!currentUser.IsAuthenticated) return (null, null);

        return currentUser.PrincipalType switch
        {
            PrincipalType.Staff => (currentUser.Id, null),
            PrincipalType.Patient => (null, currentUser.Id),
            _ => (null, null)
        };
    }

    private static string DedupeKey(NotificationType type, NotificationSubject subject, NotificationRecipient recipient)
    {
        var recipientKey = recipient.StaffMemberId?.ToString() ?? recipient.PatientAccountId!.Value.ToString();
        var occasion = subject.Occasion is null ? string.Empty : $"{subject.Occasion}:";
        return $"{EnumWire.ToWire(type)}:{subject.EntityId}:{occasion}{recipientKey}";
    }
}
