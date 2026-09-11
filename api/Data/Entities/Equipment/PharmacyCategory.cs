namespace CareLanka.Api.Data.Entities.Equipment;

// A table rather than an enum, for the same reason equipment categories are one: a sixth
// category should not need a migration. Seeded with the five from
// equipment-management-plan.md section 1.2.
public class PharmacyCategory : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    // Set per category at seed time. Prescription Medicines is true; OTC and Medical
    // Supplies are false. We record the rule, we never decide what a patient should be
    // given - that is clinical staff's call, see plan section 1.
    public bool RequiresPrescription { get; set; }

    public ICollection<PharmacyItem> Items { get; set; } = new List<PharmacyItem>();
}
