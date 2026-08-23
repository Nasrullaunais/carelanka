using System.Linq.Expressions;
using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Data;

/// <summary>
/// The one DbContext for the whole application.
///
/// READ THIS BEFORE EDITING. Four people share this file, so it is written so that you
/// almost never have to:
///
///   * Entity configuration goes in your own file under Data/Configurations/{Component}/.
///     OnModelCreating picks it up automatically. Do not add configuration here.
///   * The only shared surface is the DbSet list below. Add yours at the end of YOUR
///     component's group, so two people adding entities on the same day still touch
///     different lines.
///   * Soft delete, snake_case naming, UTC timestamps and enum-as-string are applied to
///     every entity automatically by the loops below. You do not opt in.
/// </summary>
public class CareLankaDbContext(DbContextOptions<CareLankaDbContext> options) : DbContext(options)
{
    // ---- Shared / group-owned -------------------------------------------------
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ---- Emergency (Member 1) -------------------------------------------------

    // ---- Staff (Member 2) -----------------------------------------------------

    // ---- Equipment (Member 3) -------------------------------------------------

    // ---- Patient (Member 4) ---------------------------------------------------

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);

        foreach (var entityType in b.Model.GetEntityTypes())
        {
            // Enums are stored as text, not as ints. An int column is unreadable in psql
            // and silently reorders if someone inserts a value into the middle of the
            // enum. Costs a few bytes; worth it. (entity_diagram.md Open Decision 3.)
            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    property.SetProviderClrType(typeof(string));
                    property.SetMaxLength(64);
                }
            }

            // Soft-deletable entities disappear from every query unless you explicitly
            // call IgnoreQueryFilters().
            if (typeof(SoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var filter = Expression.Lambda(
                    Expression.Property(parameter, nameof(SoftDeletableEntity.IsActive)),
                    parameter);

                b.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
