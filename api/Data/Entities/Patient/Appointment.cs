using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Appointment : AuditedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public DateTimeOffset ScheduledAt { get; set; }

    public AppointmentStatus Status { get; set; }

    public string? Reason { get; set; }

    public Guid? BookedByStaffMemberId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public Guid? ConfirmedByStaffMemberId { get; set; }

    /// <summary>
    /// Set when the visit ended in an admission rather than the patient going home. It is what
    /// tells the two <c>Completed</c> outcomes apart, and what blocks a second bill: an admitted
    /// patient is billed on the admission at discharge, never here.
    /// </summary>
    public Guid? AdmissionId { get; set; }

    public Admission? Admission { get; set; }

    public string? CancellationReason { get; set; }

    /// <summary>
    /// Null when the patient cancelled it themselves, which is what tells the
    /// two apart - the app says "you cancelled this" or "the hospital did".
    /// </summary>
    public Guid? CancelledByStaffMemberId { get; set; }

    public Bill? Bill { get; set; }
}
