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
        var due = await db.NotificationDeliveries
            .Include(d => d.Notification)
            .Where(d => d.Channel == NotificationChannel.Push
                && d.Status == NotificationStatus.Queued
                && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var delivery in due)
        {
            await DeliverAsync(delivery, ct);
            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    private async Task DeliverAsync(NotificationDelivery delivery, CancellationToken ct)
    {
        var notification = delivery.Notification;
        var tokens = await db.DeviceTokens
            .Where(t => t.RevokedAt == null
                && (notification.RecipientStaffMemberId != null
                    ? t.StaffMemberId == notification.RecipientStaffMemberId
                    : t.PatientAccountId == notification.RecipientPatientAccountId))
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            delivery.Status = NotificationStatus.Failed;
            delivery.FailureReason = "no_device";
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

        delivery.AttemptCount++;
        if (delivered)
        {
            delivery.Status = NotificationStatus.Sent;
            delivery.SentAt = clock.GetUtcNow();
        }
        else if (!retryable)
        {
            delivery.Status = NotificationStatus.Failed;
            delivery.FailureReason = "no_device";
        }
        else if (delivery.AttemptCount >= _options.MaxAttempts)
        {
            delivery.Status = NotificationStatus.Failed;
            delivery.FailureReason = "gave_up";
        }
        else
        {
            var delay = TimeSpan.FromSeconds(_options.RetryBaseSeconds * Math.Pow(3, delivery.AttemptCount - 1));
            delivery.NextAttemptAt = clock.GetUtcNow() + delay;
        }
    }
}
