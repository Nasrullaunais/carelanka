using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// One row per hospital visit. For an emergency this row exists BEFORE the patient arrives,
// which is why AdmittedAt is nullable and Status starts at AwaitingBed.
public class Admission : AuditedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public AdmissionSource Source { get; set; }

    public AdmissionCategory Category { get; set; }

    public AdmissionUrgency Urgency { get; set; }

    public AdmissionStatus Status { get; set; }

    // Patient-side input to hard rule H4: an infectious patient must get an isolation-capable
    // bed. Records the clinical fact, not the consequence a rule derives from it.
    public bool IsInfectious { get; set; }

    // Recorded proof a human, not the agent, chose the care level. Non-null by design.
    public Guid CategorySetByStaffMemberId { get; set; }

    public DateTimeOffset CategorySetAt { get; set; }

    // Emergency Service's own reference, carried as their string. Not a FK: dispatches are
    // Member 1's table and it does not exist yet.
    public string? DispatchId { get; set; }

    // A PatientAccount.Id, never a Patient.Id — a bystander who calls for a stranger has a
    // login, not a medical record, and creating one for them would be wrong.
    public Guid? ReportedByUserId { get; set; }

    public DateTimeOffset? ExpectedArrivalAt { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }

    public DateTimeOffset? DischargedAt { get; set; }

    // What is still outstanding, from PatientDetailField. Field names rather than a bare
    // boolean, because "incomplete" does not tell a ward clerk what to chase.
    public List<string> MissingFields { get; set; } = new();

    // Generated and stored: cardinality(missing_fields) = 0. Safe to compute because, unlike
    // Status, it has no transition rules to enforce and cannot drift by construction.
    public bool DetailsComplete { get; private set; }

    public DateTimeOffset? DetailsCompletedAt { get; set; }

    public CancelReason? CancelReason { get; set; }

    public ICollection<BedAssignment> BedAssignments { get; set; } = new List<BedAssignment>();

    public Discharge? Discharge { get; set; }
}
