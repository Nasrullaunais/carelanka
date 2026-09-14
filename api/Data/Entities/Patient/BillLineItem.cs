using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class BillLineItem : AuditedEntity
{
    public Guid BillId { get; set; }

    public Bill Bill { get; set; } = null!;

    public BillLineSource Source { get; set; }

    public string Description { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public Guid? BedAssignmentId { get; set; }

    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2);
}
