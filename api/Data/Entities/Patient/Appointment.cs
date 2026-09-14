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
