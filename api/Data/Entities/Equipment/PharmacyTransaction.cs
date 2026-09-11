using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// Every movement of stock, and the only way quantity_on_hand ever changes. Rows are never
// edited or deleted after the fact - that is what makes the consumption report and the
// agent's usage rate trustworthy. Hence Entity rather than AuditedEntity: there is no
// updated_at, because nothing updates.
public class PharmacyTransaction : Entity
{
    public Guid PharmacyItemId { get; set; }

    public PharmacyTransactionType Type { get; set; }

    /// <summary>Always positive. Type is what gives it a sign.</summary>
    public int Quantity { get; set; }

    // Staff Management owns the person; we store the id and nothing else. Plan section 13.3.
    public Guid PerformedByStaffId { get; set; }

    public string? Note { get; set; }
}
