using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

// A category as the administrator sees it when tidying the list: how many items still use it
// decides whether it can be removed.
public class EquipmentCategoryUsage
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Items on the register in this category, including retired ones and ones still
    /// awaiting confirmation. Removed items do not count.</summary>
    [Required]
    public int ItemCount { get; set; }
}
