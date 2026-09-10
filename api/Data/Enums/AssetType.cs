namespace CareLanka.Api.Data.Enums;

// What a maintenance schedule is against. Polymorphic so equipment and bed servicing share
// one scheduling flow instead of two nearly identical ones.
public enum AssetType
{
    EquipmentItem,
    Bed
}
