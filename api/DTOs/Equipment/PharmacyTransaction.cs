using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class PharmacyTransaction
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid PharmacyItemId { get; set; }

    public Guid? PharmacyBatchId { get; set; }

    /// <summary>Which delivery it came out of - 1 for the first, and so on.</summary>
    public int? BatchNumber { get; set; }

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
