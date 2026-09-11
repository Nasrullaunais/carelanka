using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One movement of stock. Immutable once written, which is why there is no updated_at.</summary>
public class PharmacyTransaction
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid PharmacyItemId { get; set; }

    [Required]
    public PharmacyTransactionType Type { get; set; }

    /// <summary>Always positive. The type is what gives it a sign.</summary>
    [Required]
    public int Quantity { get; set; }

    /// <summary>Staff Management owns the person; this is the id and nothing more.</summary>
    [Required]
    public Guid PerformedByStaffId { get; set; }

    public string? Note { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
