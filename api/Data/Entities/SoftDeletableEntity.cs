namespace CareLanka.Api.Data.Entities;

/// <summary>
/// An entity that history points at. Hard-deleting one would either cascade-destroy that
/// history or be blocked by a foreign key, so it is deactivated instead.
/// <para>
/// Every soft-deletable table gets a global query filter on <see cref="IsActive"/>, and
/// <strong>every unique index on one must be scoped <c>WHERE is_active</c></strong>. A
/// plain <c>UNIQUE</c> is a live bug: the filter hides the conflicting row, so the
/// service-layer duplicate check passes and <c>SaveChanges</c> throws instead.
/// </para>
/// </summary>
public abstract class SoftDeletableEntity : AuditedEntity
{
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DeletedAt { get; set; }
}
