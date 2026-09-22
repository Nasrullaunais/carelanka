using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

public class PharmacyTransaction : Entity
{
    public Guid PharmacyItemId { get; set; }

    /// <summary>The batch this movement came out of or went into. Null for movements recorded
    /// before stock was tracked by batch.</summary>
    public Guid? PharmacyBatchId { get; set; }

    public PharmacyTransactionType Type { get; set; }

    public int Quantity { get; set; }

    public Guid PerformedByStaffId { get; set; }

    public string? Note { get; set; }
}
