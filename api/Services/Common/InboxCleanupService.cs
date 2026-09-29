using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

public interface IInboxCleanupService
{
    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class InboxCleanupService(
    CareLankaDbContext db,
    TimeProvider clock,
    IOptions<NotificationOptions> options) : IInboxCleanupService
{
    // Deliveries are removed with their notification by the database's cascade.
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return db.Notifications
            .Where(n => n.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
