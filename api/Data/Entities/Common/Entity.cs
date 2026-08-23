namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// Root of every entity in CareLanka. See docs/entity_diagram.md "Base Classes".
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; }

    /// <summary>Set by the audit interceptor on insert. Always UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
