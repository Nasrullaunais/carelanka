using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// One tickable box. Rows are seeded alongside the Discharge row at admission time, so the
// checklist exists before anyone needs it.
public class DischargeChecklistItem : AuditedEntity
{
    public Guid DischargeId { get; set; }

    public Discharge Discharge { get; set; } = null!;

    public DischargeChecklistItemType ItemType { get; set; }

    // A non-mandatory item can stay unticked without blocking the discharge.
    public bool IsMandatory { get; set; }

    // Null means unticked. One nullable timestamp rather than a bool plus a timestamp that
    // can contradict it.
    public DateTimeOffset? TickedAt { get; set; }

    public Guid? TickedByStaffMemberId { get; set; }

    public string? Notes { get; set; }
}
