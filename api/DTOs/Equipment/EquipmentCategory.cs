using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One of the equipment categories. A table, not an enum, so a sixth can be added without a migration.</summary>
public class EquipmentCategory
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
