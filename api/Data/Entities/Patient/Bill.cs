using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Bill : AuditedEntity
{
    /// <summary>
    /// Exactly one of <see cref="AdmissionId"/> and <see cref="AppointmentId"/>
    /// is set, enforced by a check constraint. A patient seen at a booked
    /// appointment and sent home is billed without ever being admitted.
    /// </summary>
    public Guid? AdmissionId { get; set; }

    public Admission? Admission { get; set; }

    public Guid? AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    public string BillNumber { get; set; } = null!;

    public Guid? RaisedByStaffMemberId { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid? SettledByStaffMemberId { get; set; }

    public string? SettlementNote { get; set; }

    public ICollection<BillLineItem> LineItems { get; set; } = new List<BillLineItem>();

    public bool IsSettled => SettledAt is not null;
}
