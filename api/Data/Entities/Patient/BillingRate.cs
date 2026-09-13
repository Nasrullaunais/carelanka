using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class BillingRate : SoftDeletableEntity
{
    public WardType WardType { get; set; }

    public string ExpenseKey { get; set; } = null!;

    public decimal Amount { get; set; }
}
