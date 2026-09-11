using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/pharmacy-items.</summary>
public class CreatePharmacyItemRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [MaxLength(150)]
    public string? Manufacturer { get; set; }

    [MaxLength(50)]
    public string? BatchNumber { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>Opening stock. Every later change is a transaction, never a direct edit.</summary>
    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderThreshold { get; set; } = 10;

    public decimal? UnitPrice { get; set; }
}
