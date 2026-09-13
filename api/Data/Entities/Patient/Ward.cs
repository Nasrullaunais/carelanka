using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Ward : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public WardType WardType { get; set; }

    public GenderPolicy GenderPolicy { get; set; }
}
