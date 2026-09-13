using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

public class PharmacyTransaction : Entity
{
    public Guid PharmacyItemId { get; set; }

    public PharmacyTransactionType Type { get; set; }

    public int Quantity { get; set; }

    public Guid PerformedByStaffId { get; set; }

    public string? Note { get; set; }
}
