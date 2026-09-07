using CareLanka.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareLanka.Api.Common.Persistence;

/// <summary>
/// Stamps <c>CreatedAt</c> and <c>UpdatedAt</c> so no service ever has to remember to.
/// <para>
/// This is not the audit interceptor — that one writes <c>audit_logs</c> rows and comes
/// with the audit work. This one only fills in the two timestamp columns every entity
/// inherits.
/// </para>
/// <para>
/// Always <see cref="DateTimeOffset.UtcNow"/>: Npgsql rejects a local or unspecified
/// offset, and it fails at write time rather than at compile time.
/// </para>
/// </summary>
public sealed class TimestampInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditedEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
