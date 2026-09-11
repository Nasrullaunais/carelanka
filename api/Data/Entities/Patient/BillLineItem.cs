using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// One line on a bill. The unit price is copied here when the line is written, never looked up
// when the bill is read - so changing a rate next month leaves last month's bills exactly as
// the patient was charged.
//
// There is no LineTotal column either: it is Quantity x UnitPrice, and storing it is one more
// number that can drift away from the two it came from.
public class BillLineItem : AuditedEntity
{
    public Guid BillId { get; set; }

    public Bill Bill { get; set; } = null!;

    public BillLineSource Source { get; set; }

    public string Description { get; set; } = null!;

    // Days for a bed, 1 for a fee, whatever reception typed for a manual charge.
    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    // Which bed assignment this line prices, for a bed_stay line. No FK on purpose - the line
    // has to survive as written even if the assignment it came from is later rewritten, and it
    // is only here so preparing the bill again can match a line to its stay.
    public Guid? BedAssignmentId { get; set; }

    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2);
}
