using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

public class Bed : SoftDeletableEntity
{
    public Guid WardId { get; set; }

    public string BedNumber { get; set; } = null!;

    public bool HasIsolation { get; set; }

    public int NurseStationDistance { get; set; } = 1;

    public BedCondition Condition { get; set; }

    public string? AssetTag { get; set; }
}
