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

    public string? BatchNumber { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [Required]
    public string Unit { get; set; } = string.Empty;

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
