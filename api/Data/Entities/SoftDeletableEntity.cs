namespace CareLanka.Api.Data.Entities;

public abstract class SoftDeletableEntity : AuditedEntity
{
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DeletedAt { get; set; }
}
