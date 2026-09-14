using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class EquipmentItem : EquipmentItemSummary
{
    [Required]
    public DateOnly PurchaseDate { get; set; }

    public string? SerialNumber { get; set; }

    public Guid? AssignedToAdmissionId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
