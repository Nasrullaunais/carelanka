using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// The third arrival path: they booked beforehand. On check-in this becomes an Admission
// with Source = PreRegistered.
public class Appointment : AuditedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public DateTimeOffset ScheduledAt { get; set; }

    public AppointmentStatus Status { get; set; }

    // Free text, written by whoever booked. Shown to staff and never read by the bed agent —
    // free text stays data, never instructions.
    public string? Reason { get; set; }

    // Null for a self-booking from the patient app. Id only: the name lives in StaffMember
    // and is resolved through Staff's POST /staff/lookup at read time.
    public Guid? BookedByStaffMemberId { get; set; }

    // Set once checked in. The Admission does not point back — one link, one owner.
    public Guid? AdmissionId { get; set; }

    public Admission? Admission { get; set; }
}
