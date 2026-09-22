using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class BedAssignment : AuditedEntity
{
    public Guid AdmissionId { get; set; }

    public Admission Admission { get; set; } = null!;

    public Guid BedId { get; set; }

    public AssignmentStatus Status { get; set; }

    public DateTimeOffset? ReservedUntil { get; set; }

    public DateTimeOffset? OccupiedAt { get; set; }

    public bool IsDowngrade { get; set; }

    public Guid? ApprovedByStaffMemberId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? OverrideReason { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public ReleaseReason? ReleaseReason { get; set; }
}
