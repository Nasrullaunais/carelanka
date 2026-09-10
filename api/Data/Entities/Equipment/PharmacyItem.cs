namespace CareLanka.Api.Data.Entities.Equipment;

// One catalog entry and its current quantity, in one row. A single central pharmacy store
// rather than per-ward stock, which plan section 15 records as a deliberate simplification
// and section 16 leaves open for the group.
public class PharmacyItem : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public PharmacyCategory Category { get; set; } = null!;

    public string? Manufacturer { get; set; }

    // Medicines are tracked by batch in a real pharmacy, and a recall is by batch number.
    public string? BatchNumber { get; set; }

    // Drives the medicine_expiring warning. Null for things that do not expire, which is
    // why the expiry index is filtered rather than covering every row.
    public DateOnly? ExpiryDate { get; set; }

    public string Unit { get; set; } = null!;

    // Never written directly by a caller. Every change is a PharmacyTransaction applied
    // through one conditional UPDATE, so it cannot go negative and two people dispensing
    // at once cannot both succeed past zero. See PharmacyItemService.
    public int QuantityOnHand { get; set; }

    public int ReorderThreshold { get; set; }

    public decimal? UnitPrice { get; set; }

    public ICollection<PharmacyTransaction> Transactions { get; set; } = new List<PharmacyTransaction>();
}
