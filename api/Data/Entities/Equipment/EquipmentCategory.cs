namespace CareLanka.Api.Data.Entities.Equipment;

// A table rather than an enum, so the Administrator can add a sixth category without a
// migration. Seeded with the five from equipment-management-plan.md section 1.1.
public class EquipmentCategory : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public ICollection<EquipmentItem> Items { get; set; } = new List<EquipmentItem>();
}
