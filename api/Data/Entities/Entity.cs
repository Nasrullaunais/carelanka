namespace CareLanka.Api.Data.Entities;

public abstract class Entity
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
