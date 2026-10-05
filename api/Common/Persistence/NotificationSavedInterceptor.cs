using System.Data.Common;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Hubs.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareLanka.Api.Common.Persistence;

// Scoped, not singleton: it holds the recipients of this request's save between the "before"
// and "after" hooks, and a singleton instance would leak that state across concurrent requests.
// Inside a transaction the announcement waits for the commit; sent earlier, a client re-reading its
// inbox straight away would still see the old rows.
public sealed class NotificationSavedInterceptor(IHubContext<NotificationsHub> hub)
    : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private List<string> _pendingGroups = [];
    private readonly HashSet<string> _awaitingCommit = [];

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
        AnnounceAsync(TakeAnnounceable(eventData.Context)).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await AnnounceAsync(TakeAnnounceable(eventData.Context));
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        _awaitingCommit.Clear();
        return result;
    }

    public ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        _awaitingCommit.Clear();
        return ValueTask.FromResult(result);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => AnnounceAsync(TakeCommitted()).GetAwaiter().GetResult();

    public Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        => AnnounceAsync(TakeCommitted());

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => _awaitingCommit.Clear();

    public Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _awaitingCommit.Clear();
        return Task.CompletedTask;
    }

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => _awaitingCommit.Clear();

    public Task TransactionFailedAsync(
        DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _awaitingCommit.Clear();
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        // Whether exactly one recipient column is set is the database check constraint's job
        // (PushDeliveryTests asserts it throws DbUpdateException) - this only reads entries that
        // already satisfy it, so a malformed row fails at the constraint, not in here first.
        _pendingGroups = context.ChangeTracker.Entries<Notification>()
            .Where(entry => entry.State == EntityState.Added
                || (entry.State == EntityState.Modified && entry.Property(n => n.ReadAt).IsModified))
            .Where(entry => entry.Entity.RecipientStaffMemberId is not null
                || entry.Entity.RecipientPatientAccountId is not null)
            .Select(entry => entry.Entity.RecipientStaffMemberId is { } staffId
                ? NotificationGroups.For(Data.Enums.PrincipalType.Staff, staffId)
                : NotificationGroups.For(Data.Enums.PrincipalType.Patient, entry.Entity.RecipientPatientAccountId!.Value))
            .Distinct()
            .ToList();
    }

    private List<string> TakeAnnounceable(DbContext? context)
    {
        var groups = _pendingGroups;
        _pendingGroups = [];

        if (context?.Database.CurrentTransaction is null) return groups;

        _awaitingCommit.UnionWith(groups);
        return [];
    }

    private List<string> TakeCommitted()
    {
        var groups = _awaitingCommit.ToList();
        _awaitingCommit.Clear();
        return groups;
    }

    private async Task AnnounceAsync(List<string> groups)
    {
        foreach (var group in groups)
        {
            await hub.Clients.Group(group).SendAsync("inboxChanged");
        }
    }
}
