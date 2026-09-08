namespace CareLanka.Api.Data.Entities;

public abstract class AuditedEntity : Entity
{
    public DateTimeOffset UpdatedAt { get; set; }
}
