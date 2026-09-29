using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface IInboxService
{
    Task<PagedResult<InboxNotification>> ListMyNotificationsAsync(
        ListMyNotificationsQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<UnreadNotificationCount> GetMyUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<InboxNotification> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UnreadNotificationCount> MarkAllReadAsync(CancellationToken cancellationToken = default);
}
