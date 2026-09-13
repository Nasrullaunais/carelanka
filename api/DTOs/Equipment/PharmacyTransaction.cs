using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class PharmacyTransaction
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid PharmacyItemId { get; set; }

    [Required]
    public PharmacyTransactionType Type { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public Guid PerformedByStaffId { get; set; }

    public string? Note { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
