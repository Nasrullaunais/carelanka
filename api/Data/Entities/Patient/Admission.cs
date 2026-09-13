using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Admission : AuditedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public AdmissionSource Source { get; set; }

    public AdmissionCategory Category { get; set; }

    public AdmissionUrgency Urgency { get; set; }

    public AdmissionStatus Status { get; set; }

    public bool IsInfectious { get; set; }

    public Guid CategorySetByStaffMemberId { get; set; }

    public DateTimeOffset CategorySetAt { get; set; }

    public string? DispatchId { get; set; }

    public Guid? ReportedByUserId { get; set; }

    public DateTimeOffset? ExpectedArrivalAt { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }

    public DateTimeOffset? DischargedAt { get; set; }

    public List<string> MissingFields { get; set; } = new();

    public bool DetailsComplete { get; private set; }

    public DateTimeOffset? DetailsCompletedAt { get; set; }

    public CancelReason? CancelReason { get; set; }

    public string? CancelNote { get; set; }

    public ICollection<BedAssignment> BedAssignments { get; set; } = new List<BedAssignment>();

    public Discharge? Discharge { get; set; }
}
