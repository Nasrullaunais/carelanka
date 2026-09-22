using System.ComponentModel.DataAnnotations;
namespace CareLanka.Api.DTOs.Equipment;

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

    [Required]
    public string Unit { get; set; } = string.Empty;

    /// <summary>How many deliveries of this medicine are on the shelf.</summary>
    [Required]
    public int BatchCount { get; set; }

    /// <summary>The soonest expiry date among batches that still have stock.</summary>
    public DateOnly? EarliestExpiry { get; set; }

    [Required]
    public int QuantityOnHand { get; set; }

    [Required]
    public int ReorderThreshold { get; set; }

    public decimal? UnitPrice { get; set; }

    [Required]
    public bool IsAvailable { get; set; }

    [Required]
    public bool BelowThreshold { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
