using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class PharmacyBatch
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid PharmacyItemId { get; set; }

    [Required]
    public int BatchNumber { get; set; }

    public string? Reference { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [Required]
    public int QuantityOnHand { get; set; }

    public string? Note { get; set; }

    /// <summary>When this delivery was recorded.</summary>
    [Required]
    public DateTimeOffset ReceivedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
