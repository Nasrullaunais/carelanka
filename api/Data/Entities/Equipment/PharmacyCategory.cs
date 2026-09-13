namespace CareLanka.Api.Data.Entities.Equipment;

public class PharmacyCategory : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public bool RequiresPrescription { get; set; }

    public ICollection<PharmacyItem> Items { get; set; } = new List<PharmacyItem>();
}
