using CareLanka.Api.Data.Entities.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareLanka.Api.Common.Persistence;

public sealed class AmbulanceStatusHistoryInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
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

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var now = timeProvider.GetUtcNow();
        var changes = context.ChangeTracker.Entries<Ambulance>()
            .Where(entry => entry.State == EntityState.Added
                || entry.State == EntityState.Modified && entry.Property(ambulance => ambulance.Status).IsModified)
            .Where(entry => !context.ChangeTracker.Entries<AmbulanceStatusHistory>().Any(history =>
                history.State == EntityState.Added
                && history.Entity.AmbulanceId == entry.Entity.Id
                && history.Entity.Status == entry.Entity.Status))
            .Select(entry => new AmbulanceStatusHistory
            {
                Id = Guid.NewGuid(),
                AmbulanceId = entry.Entity.Id,
                Status = entry.Entity.Status,
                StartedAt = now
            })
            .ToList();

        context.AddRange(changes);
    }
}
