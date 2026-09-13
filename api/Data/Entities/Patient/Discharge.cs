using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Discharge : AuditedEntity
{
    public Guid AdmissionId { get; set; }

    public Admission Admission { get; set; } = null!;

    public AssignedBy FlaggedBy { get; set; }

    public DateTimeOffset FlaggedAt { get; set; }

    public Guid? ConfirmedByStaffMemberId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public string? SummaryNote { get; set; }

    public ICollection<DischargeChecklistItem> ChecklistItems { get; set; }
        = new List<DischargeChecklistItem>();
}
