namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// For entities whose rows are mutated after insert (status transitions, field edits).
/// </summary>
public abstract class AuditedEntity : Entity
{
    /// <summary>Set by the audit interceptor on insert and on every update. Always UTC.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
