using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

public sealed class PushDeliveryProcessor(
    CareLankaDbContext db,
    IPushSender sender,
    TimeProvider clock,
    IOptions<PushOptions> options)
{
    private readonly PushOptions _options = options.Value;

    public async Task<int> DeliverDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var due = await db.Notifications
            .Where(n => n.Channel == NotificationChannel.Push
                && n.Status == NotificationStatus.Queued
                && n.NextAttemptAt <= now)
            .OrderBy(n => n.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var notification in due)
        {
            await DeliverAsync(notification, ct);
            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    private async Task DeliverAsync(Notification notification, CancellationToken ct)
    {
        var tokens = await db.DeviceTokens
            .Where(t => t.StaffMemberId == notification.RecipientStaffMemberId && t.RevokedAt == null)
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            notification.Status = NotificationStatus.Failed;
            notification.FailureReason = "no_device";
            return;
        }

        var message = new PushMessage(notification.Title, notification.Body, notification.DedupeKey,
            new Dictionary<string, string>
            {
                ["entity_type"] = notification.EntityType ?? string.Empty,
                ["entity_id"] = notification.EntityId?.ToString() ?? string.Empty
            });

        var delivered = false;
        var retryable = false;
        foreach (var token in tokens)
        {
            switch (await sender.SendAsync(token.Token, message, ct))
            {
                case PushOutcome.Delivered:
                    delivered = true;
                    break;
                case PushOutcome.TokenRejected:
                    token.RevokedAt = clock.GetUtcNow();
                    break;
                default:
                    retryable = true;
                    break;
            }
        }

        notification.AttemptCount++;
        if (delivered)
        {
            notification.Status = NotificationStatus.Sent;
            notification.SentAt = clock.GetUtcNow();
        }
        else if (!retryable)
        {
            notification.Status = NotificationStatus.Failed;
            notification.FailureReason = "no_device";
        }
        else if (notification.AttemptCount >= _options.MaxAttempts)
        {
            notification.Status = NotificationStatus.Failed;
            notification.FailureReason = "gave_up";
        }
        else
        {
            var delay = TimeSpan.FromSeconds(_options.RetryBaseSeconds * Math.Pow(3, notification.AttemptCount - 1));
            notification.NextAttemptAt = clock.GetUtcNow() + delay;
        }
    }
}
