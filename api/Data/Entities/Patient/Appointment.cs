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
}
