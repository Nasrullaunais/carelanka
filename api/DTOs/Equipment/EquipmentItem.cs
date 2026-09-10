using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>A full equipment item. Extends the list row with the fields a detail view needs.</summary>
public class EquipmentItem : EquipmentItemSummary
{
    [Required]
    public DateOnly PurchaseDate { get; set; }

    public string? SerialNumber { get; set; }

    /// <summary>Set while the status is assigned. Patient Management's admission, id only.</summary>
    public Guid? AssignedToAdmissionId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
