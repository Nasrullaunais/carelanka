using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Common;

public interface INotifier
{
    Task NotifyAsync(
        NotificationType type,
        Recipients to,
        NotificationSubject subject,
        CancellationToken cancellationToken = default,
        params object[] args);
}
