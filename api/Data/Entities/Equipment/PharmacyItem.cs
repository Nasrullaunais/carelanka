namespace CareLanka.Api.Data.Entities.Equipment;

public class PharmacyItem : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public PharmacyCategory Category { get; set; } = null!;

    public string? Manufacturer { get; set; }

    public string Unit { get; set; } = null!;

    /// <summary>Every batch added together, kept in step as batches change.</summary>
    public int QuantityOnHand { get; set; }

    public int ReorderThreshold { get; set; }

    public decimal? UnitPrice { get; set; }

    public ICollection<PharmacyBatch> Batches { get; set; } = new List<PharmacyBatch>();

    public ICollection<PharmacyTransaction> Transactions { get; set; } = new List<PharmacyTransaction>();
}
