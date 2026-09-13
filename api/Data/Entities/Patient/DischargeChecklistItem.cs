using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class DischargeChecklistItem : AuditedEntity
{
    public Guid DischargeId { get; set; }

    public Discharge Discharge { get; set; } = null!;

    public DischargeChecklistItemType ItemType { get; set; }

    public bool IsMandatory { get; set; }

    public DateTimeOffset? TickedAt { get; set; }

    public Guid? TickedByStaffMemberId { get; set; }

    public string? Notes { get; set; }
}
