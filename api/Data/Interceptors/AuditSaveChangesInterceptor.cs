using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareLanka.Api.Data.Interceptors;

/// <summary>
/// Stamps CreatedAt / UpdatedAt / DeletedAt and writes the AuditLog rows.
/// Nobody writes an AuditLog by hand — mark your entity IAuditLogged and it happens.
/// A soft delete is logged as Delete, not Update.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider clock)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = clock.GetUtcNow();
        var actor = currentUser.StaffMemberId;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries<Entity>().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var operation = Stamp(entry, now);
            if (entry.Entity is IAuditLogged)
            {
                logs.Add(new AuditLog
                {
                    EntityType = entry.Entity.GetType().Name,
                    EntityId = entry.Entity.Id,
                    Operation = operation,
                    PerformedByStaffMemberId = actor,
                    CreatedAt = now
                });
            }
        }

        // AuditLog rows are added after the loop so they are not themselves audited.
        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }

    private static AuditOperation Stamp(EntityEntry<Entity> entry, DateTimeOffset now)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.CreatedAt = now;
            if (entry.Entity is AuditedEntity added) added.UpdatedAt = now;
            return AuditOperation.Create;
        }

        if (entry.Entity is AuditedEntity updated) updated.UpdatedAt = now;

        // A soft delete is IsActive going true -> false. It is a Delete, not an Update.
        if (entry.Entity is SoftDeletableEntity soft)
        {
            var isActive = entry.Property(nameof(SoftDeletableEntity.IsActive));
            if (isActive.IsModified && isActive.OriginalValue is true && soft.IsActive is false)
            {
                soft.DeletedAt = now;
                return AuditOperation.Delete;
            }
        }

        return AuditOperation.Update;
    }
}
