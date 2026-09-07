namespace CareLanka.Api.Data.Entities;

/// <summary>
/// Root of every table. <c>docs/entity_diagram.md</c> "Base Classes" is the authority —
/// do not add fields here.
/// <para>
/// There are deliberately no <c>CreatedBy</c> / <c>UpdatedBy</c> columns: who did what
/// lives in <c>audit_logs</c>. Two answers to one question is worse than one.
/// </para>
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; }

    /// <summary>Set by <c>TimestampInterceptor</c>. Always UTC — Npgsql rejects anything else.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
