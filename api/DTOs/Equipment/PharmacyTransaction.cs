using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One movement of stock. Immutable once written, which is why there is no updated_at.</summary>
public class PharmacyTransaction
{
    public Guid Id { get; set; }

    public Guid PharmacyItemId { get; set; }

    public PharmacyTransactionType Type { get; set; }

    /// <summary>Always positive. The type is what gives it a sign.</summary>
    public int Quantity { get; set; }

    /// <summary>Staff Management owns the person; this is the id and nothing more.</summary>
    public Guid PerformedByStaffId { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
