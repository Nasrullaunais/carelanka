using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

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

    public Guid? WardId { get; set; }

    public string? WardName { get; set; }

    [Required]
    public EquipmentStatus Status { get; set; }

    public DateOnly? NextMaintenanceDue { get; set; }
}
