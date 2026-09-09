using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// Referenced by Shift.WardId, EquipmentItem.WardId and Dispatch.DestinationWardId, so this
// schema is frozen once agreed — changing it breaks three other components.
public class Ward : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public WardType WardType { get; set; }

    public GenderPolicy GenderPolicy { get; set; }
}
