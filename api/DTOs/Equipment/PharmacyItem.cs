namespace CareLanka.Api.DTOs.Equipment;

/// <summary>A catalog entry and how much of it is on the shelf.</summary>
public class PharmacyItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    /// <summary>Medicines are tracked by batch, which is also how a recall is issued.</summary>
    public string? BatchNumber { get; set; }

    /// <summary>Null for things that do not expire. Drives the medicine_expiring warning.</summary>
    public DateOnly? ExpiryDate { get; set; }

    public string Unit { get; set; } = string.Empty;

    public int QuantityOnHand { get; set; }

    public int ReorderThreshold { get; set; }

    public decimal? UnitPrice { get; set; }

    /// <summary>Computed as quantity_on_hand > 0, never stored, so it cannot drift out of step with the quantity.</summary>
    public bool IsAvailable { get; set; }

    /// <summary>Computed the same way. What the low-stock sweep keys off.</summary>
    public bool BelowThreshold { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
