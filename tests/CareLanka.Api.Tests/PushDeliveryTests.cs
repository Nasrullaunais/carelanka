using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class RecordingPushSender : IPushSender
{
    private readonly Queue<PushOutcome> _script = new();

    public List<(string Token, PushMessage Message)> Sent { get; } = [];

    public PushOutcome Default { get; set; } = PushOutcome.Delivered;

    public void Script(params PushOutcome[] outcomes)
    {
        foreach (var outcome in outcomes) _script.Enqueue(outcome);
    }

    public Task<PushOutcome> SendAsync(string deviceToken, PushMessage message, CancellationToken ct = default)
    {
        Sent.Add((deviceToken, message));
        return Task.FromResult(_script.Count > 0 ? _script.Dequeue() : Default);
    }
}

[Collection(ApiCollection.Name)]
public sealed class PushDeliveryTests
{
    private readonly ApiApplication _application;

    public PushDeliveryTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_queued_push_reaches_every_active_device_once_and_is_not_sent_again()
    {
        var (staffId, notificationId) = await SeedAsync("phone-a", "phone-b");
        var sender = new RecordingPushSender();

        await DeliverAsync(sender, DateTimeOffset.UtcNow);
        await DeliverAsync(sender, DateTimeOffset.UtcNow);

        Assert.Equal([$"phone-a-{staffId:N}", $"phone-b-{staffId:N}"], SentTo(sender, staffId).Select(x => x.Token).Order());
        var saved = await NotificationAsync(notificationId);
        Assert.Equal(NotificationStatus.Sent, saved.Status);
        Assert.NotNull(saved.SentAt);
        Assert.Equal(1, saved.AttemptCount);
        Assert.Equal(staffId, saved.RecipientStaffMemberId);
    }

    [Fact]
    public async Task The_message_carries_only_the_generic_text_and_the_dispatch_id()
    {
        var (_, notificationId) = await SeedAsync("phone-a");
        var sender = new RecordingPushSender();

        await DeliverAsync(sender, DateTimeOffset.UtcNow);

        var saved = await NotificationAsync(notificationId);
        var message = SentTo(sender, saved.RecipientStaffMemberId).Single().Message;
        Assert.Equal(saved.Title, message.Title);
        Assert.Equal(saved.Body, message.Body);
        Assert.Equal(["entity_id", "entity_type"], message.Data.Keys.Order());
        Assert.Equal(saved.EntityId.ToString(), message.Data["entity_id"]);
    }

    [Fact]
    public async Task A_failed_send_is_retried_later_and_then_succeeds()
    {
        var (staffId, notificationId) = await SeedAsync("phone-a");
        var sender = new RecordingPushSender();
        sender.Script(PushOutcome.Failed);
        var now = DateTimeOffset.UtcNow;

        await DeliverAsync(sender, now);
        var afterFirst = await NotificationAsync(notificationId);
        Assert.Equal(NotificationStatus.Queued, afterFirst.Status);
        Assert.True(afterFirst.NextAttemptAt > now);

        await DeliverAsync(sender, now);
        Assert.Single(SentTo(sender, staffId));

        await DeliverAsync(sender, afterFirst.NextAttemptAt.AddSeconds(1));
        Assert.Equal(NotificationStatus.Sent, (await NotificationAsync(notificationId)).Status);
        Assert.Equal(2, SentTo(sender, staffId).Count);
    }

    [Fact]
    public async Task A_push_that_keeps_failing_is_given_up_on_and_marked_failed()
    {
        var (_, notificationId) = await SeedAsync("phone-a");
        var sender = new RecordingPushSender { Default = PushOutcome.Failed };
        var when = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await DeliverAsync(sender, when);
            when = (await NotificationAsync(notificationId)).NextAttemptAt.AddSeconds(1);
        }

        var saved = await NotificationAsync(notificationId);
        Assert.Equal(NotificationStatus.Failed, saved.Status);
        Assert.Equal("gave_up", saved.FailureReason);
        Assert.Equal(5, saved.AttemptCount);
    }

    [Fact]
    public async Task A_token_the_provider_rejects_is_revoked_and_the_push_fails_without_retrying()
    {
        var (staffId, notificationId) = await SeedAsync("stale-phone");
        var sender = new RecordingPushSender { Default = PushOutcome.TokenRejected };

        await DeliverAsync(sender, DateTimeOffset.UtcNow);

        Assert.Equal(NotificationStatus.Failed, (await NotificationAsync(notificationId)).Status);
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.NotNull((await db.DeviceTokens.SingleAsync(x => x.Token == $"stale-phone-{staffId:N}")).RevokedAt);
    }

    [Fact]
    public async Task A_person_with_no_registered_device_gets_a_failed_push_so_polling_covers_it()
    {
        var (staffId, notificationId) = await SeedAsync();
        var sender = new RecordingPushSender();

        await DeliverAsync(sender, DateTimeOffset.UtcNow);

        var saved = await NotificationAsync(notificationId);
        Assert.Equal(NotificationStatus.Failed, saved.Status);
        Assert.Equal("no_device", saved.FailureReason);
        Assert.Empty(SentTo(sender, staffId));
    }

    [Fact]
    public async Task A_second_push_with_the_same_key_is_refused_by_the_database()
    {
        var (staffId, notificationId) = await SeedAsync();
        var original = await NotificationAsync(notificationId);
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        db.Notifications.Add(Copy(original, staffId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static List<(string Token, PushMessage Message)> SentTo(RecordingPushSender sender, Guid staffId)
        => sender.Sent.Where(x => x.Token.EndsWith($"-{staffId:N}")).ToList();

    private async Task DeliverAsync(RecordingPushSender sender, DateTimeOffset now)
    {
        using var scope = _application.Services.CreateScope();
        var processor = new PushDeliveryProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            sender,
            new FixedClock(now),
            Options.Create(new PushOptions { BatchSize = 1000 }));
        await processor.DeliverDueAsync();
    }

    private async Task<Notification> NotificationAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Notifications.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<(Guid StaffId, Guid NotificationId)> SeedAsync(params string[] tokens)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(), Email = $"push-{Guid.NewGuid():N}@carelanka.invalid", PasswordHash = "x",
            FirstName = "Push", LastName = "Test", Role = StaffRole.AmbulanceCrew, IsActive = true
        };
        db.StaffMembers.Add(staff);
        foreach (var token in tokens)
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                Id = Guid.NewGuid(), StaffMemberId = staff.Id, Token = $"{token}-{staff.Id:N}",
                Platform = DevicePlatform.Android, LastSeenAt = DateTimeOffset.UtcNow
            });
        }

        var entityId = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<IPushNotifications>()
            .Stage([staff.Id], "New ambulance assignment", "Open CareLanka to see your run.", "dispatch", entityId, "dispatch-assigned");
        await db.SaveChangesAsync();
        var id = await db.Notifications.Where(x => x.EntityId == entityId).Select(x => x.Id).SingleAsync();
        return (staff.Id, id);
    }

    private static Notification Copy(Notification source, Guid staffId) => new()
    {
        Id = Guid.NewGuid(), RecipientStaffMemberId = staffId, Channel = source.Channel, Title = source.Title,
        Body = source.Body, EntityType = source.EntityType, EntityId = source.EntityId, Status = source.Status,
        DedupeKey = source.DedupeKey, NextAttemptAt = source.NextAttemptAt
    };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
