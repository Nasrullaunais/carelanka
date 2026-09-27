using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Common;

public sealed class InboxService(CareLankaDbContext db, ICurrentUser currentUser, TimeProvider clock) : IInboxService
{
    public async Task<PagedResult<InboxNotification>> ListMyNotificationsAsync(
        ListMyNotificationsQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = MyNotifications().AsNoTracking();
        if (parameters.UnreadOnly) query = query.Where(n => n.ReadAt == null);

        var totalItems = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(ToInboxNotification).ToList();
        return PagedResult<InboxNotification>.From(items, parameters.Page, parameters.PageSize, totalItems);
    }

    public async Task<UnreadNotificationCount> GetMyUnreadCountAsync(CancellationToken cancellationToken = default)
        => new() { Count = await MyNotifications().CountAsync(n => n.ReadAt == null, cancellationToken) };

    public async Task<InboxNotification> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await MyNotifications().FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), id);

        notification.ReadAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return ToInboxNotification(notification);
    }

    public async Task<UnreadNotificationCount> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        await MyNotifications()
            .Where(n => n.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, now), cancellationToken);

        return new UnreadNotificationCount { Count = 0 };
    }

    private IQueryable<Notification> MyNotifications() => currentUser.PrincipalType switch
    {
        PrincipalType.Staff => db.Notifications.Where(n => n.RecipientStaffMemberId == currentUser.Id),
        PrincipalType.Patient => db.Notifications.Where(n => n.RecipientPatientAccountId == currentUser.Id),
        _ => db.Notifications.Where(_ => false)
    };

    private static InboxNotification ToInboxNotification(Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Title = n.Title,
        Body = n.Body,
        EntityType = n.EntityType,
        EntityId = n.EntityId,
        ReadAt = n.ReadAt,
        CreatedAt = n.CreatedAt
    };
}
