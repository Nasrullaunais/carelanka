using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>An equipment item as a list row.</summary>
public class EquipmentItemSummary
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = string.Empty;

    [Required]
    public string Manufacturer { get; set; } = string.Empty;

    [Required]
    public string AssetTag { get; set; } = string.Empty;

    /// <summary>Null means the central store rather than a ward.</summary>
    public Guid? WardId { get; set; }

    /// <summary>Read from Patient Management. Null when the item is in the central store.</summary>
    public string? WardName { get; set; }

    [Required]
    public EquipmentStatus Status { get; set; }

    public DateOnly? NextMaintenanceDue { get; set; }
}
