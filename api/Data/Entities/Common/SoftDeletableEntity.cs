namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// For entities that are FK targets of historical records — hard-deleting them would
/// either cascade-destroy history or be blocked by the FK constraint.
/// Filtered out of every query by a global query filter; set IsActive = false to delete.
/// </summary>
public abstract class SoftDeletableEntity : AuditedEntity
{
    public bool IsActive { get; set; } = true;

    /// <summary>Stamped by the audit interceptor when IsActive goes true to false.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
