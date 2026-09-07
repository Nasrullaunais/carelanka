namespace CareLanka.Api.Data.Entities;

/// <summary>Any entity whose rows are mutated after insert — status transitions, field edits.</summary>
public abstract class AuditedEntity : Entity
{
    /// <summary>Set by <c>TimestampInterceptor</c> on insert and on every update.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
