namespace CareLanka.Api.Data.Entities.Equipment;

public class EquipmentCategory : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public ICollection<EquipmentItem> Items { get; set; } = new List<EquipmentItem>();
}
