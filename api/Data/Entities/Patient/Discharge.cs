using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// The 1:1 companion row, created at admission time rather than when discharge happens. That
// gives clinical staff somewhere to record readiness during the stay, and the agent a
// persistent target to watch.
public class Discharge : AuditedEntity
{
    public Guid AdmissionId { get; set; }

    public Admission Admission { get; set; } = null!;

    // Whether the agent flagged this stay as nearing discharge, or a human did.
    public AssignedBy FlaggedBy { get; set; }

    public DateTimeOffset FlaggedAt { get; set; }

    public Guid? ConfirmedByStaffMemberId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public string? SummaryNote { get; set; }

    // Readiness is these rows, not a column. "All mandatory items ticked" is a query over
    // them, so the two can never disagree.
    public ICollection<DischargeChecklistItem> ChecklistItems { get; set; }
        = new List<DischargeChecklistItem>();
}
