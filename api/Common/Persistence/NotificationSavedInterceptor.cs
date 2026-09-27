using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Hubs.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareLanka.Api.Common.Persistence;

// Scoped, not singleton: it holds the recipients of this request's save between the "before"
// and "after" hooks, and a singleton instance would leak that state across concurrent requests.
public sealed class NotificationSavedInterceptor(IHubContext<NotificationsHub> hub) : SaveChangesInterceptor
{
    private List<string> _pendingGroups = [];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Announce();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await AnnounceAsync();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        // Whether exactly one recipient column is set is the database check constraint's job
        // (PushDeliveryTests asserts it throws DbUpdateException) - this only reads entries that
        // already satisfy it, so a malformed row fails at the constraint, not in here first.
        _pendingGroups = context.ChangeTracker.Entries<Notification>()
            .Where(entry => entry.State == EntityState.Added)
            .Where(entry => entry.Entity.RecipientStaffMemberId is not null
                || entry.Entity.RecipientPatientAccountId is not null)
            .Select(entry => entry.Entity.RecipientStaffMemberId is { } staffId
                ? NotificationGroups.For(Data.Enums.PrincipalType.Staff, staffId)
                : NotificationGroups.For(Data.Enums.PrincipalType.Patient, entry.Entity.RecipientPatientAccountId!.Value))
            .Distinct()
            .ToList();
    }

    private void Announce() => AnnounceAsync().GetAwaiter().GetResult();

    private async Task AnnounceAsync()
    {
        var groups = _pendingGroups;
        _pendingGroups = [];

        foreach (var group in groups)
        {
            await hub.Clients.Group(group).SendAsync("inboxChanged");
        }
    }
}
