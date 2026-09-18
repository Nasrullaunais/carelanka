namespace CareLanka.Api.Data.Entities.Equipment;

// One delivery of a medicine: the boxes that arrived together and the expiry date printed on them.
// Stock lives here rather than on the item, because two deliveries of the same medicine expire on
// different days and the one expiring first has to go out first.
public class PharmacyBatch : AuditedEntity
{
    public Guid PharmacyItemId { get; set; }

    public PharmacyItem Item { get; set; } = null!;

    /// <summary>1 for the first delivery of this medicine, 2 for the next, and so on.</summary>
    public int BatchNumber { get; set; }

    /// <summary>The manufacturer's batch code from the box, when there is one.</summary>
    public string? Reference { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public int QuantityOnHand { get; set; }

    public string? Note { get; set; }
}
