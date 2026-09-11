using System.ComponentModel.DataAnnotations;
namespace CareLanka.Api.DTOs.Equipment;

/// <summary>A catalog entry and how much of it is on the shelf.</summary>
public class PharmacyItem
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public string CategoryName { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    /// <summary>Medicines are tracked by batch, which is also how a recall is issued.</summary>
    public string? BatchNumber { get; set; }

    /// <summary>Null for things that do not expire. Drives the medicine_expiring warning.</summary>
    public DateOnly? ExpiryDate { get; set; }

    [Required]
    public string Unit { get; set; } = string.Empty;

    [Required]
    public int QuantityOnHand { get; set; }

    [Required]
    public int ReorderThreshold { get; set; }

    public decimal? UnitPrice { get; set; }

    /// <summary>Computed as quantity_on_hand > 0, never stored, so it cannot drift out of step with the quantity.</summary>
    [Required]
    public bool IsAvailable { get; set; }

    /// <summary>Computed the same way. What the low-stock sweep keys off.</summary>
    [Required]
    public bool BelowThreshold { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
