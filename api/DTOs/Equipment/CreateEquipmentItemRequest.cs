using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class CreateEquipmentItemRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Manufacturer { get; set; } = string.Empty;

    [Required]
    public DateOnly PurchaseDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string AssetTag { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public Guid? WardId { get; set; }

    public DateOnly? NextMaintenanceDue { get; set; }
}
