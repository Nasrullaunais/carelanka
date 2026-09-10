using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// Who is in which bed, and the hold on a bed nobody is in yet. One row walks
// Reserved -> Occupied -> Released; several rows per Admission cover mid-stay transfers.
//
// The exclusivity guarantee is the two partial unique indexes in the configuration, not any
// check in a service. Two nurses assigning the same bed at the same instant both pass an
// application-level "is it free?" test.
public class BedAssignment : AuditedEntity
{
    public Guid AdmissionId { get; set; }

    public Admission Admission { get; set; } = null!;

    // No FK: beds are Health Equipment's table (Member 3) and it does not exist yet.
    // Ward, bed number, condition and isolation are read from their register, never stored here.
    public Guid BedId { get; set; }

    public AssignmentStatus Status { get; set; }

    // The expiring hold. Past this instant the bed is free again with no human action.
    // Null once the patient is actually in the bed.
    public DateTimeOffset? ReservedUntil { get; set; }

    public AssignedBy AssignedBy { get; set; }

    // No FK: AgentWorkflow is common and has not been built. Null when a human picked the bed.
    public Guid? WorkflowId { get; set; }

    // A real column and not a note in the workflow payload, because it routes the approval —
    // a downgrade is Duty Manager only, so it has to be queryable and auditable.
    public bool IsDowngrade { get; set; }

    public Guid? ApprovedByStaffMemberId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    // Why a human overrode the agent's proposal. Staff-facing.
    public string? OverrideReason { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public ReleaseReason? ReleaseReason { get; set; }
}
