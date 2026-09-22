using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// A photo or PDF of a doctor's prescription a patient sends from the app, so the pharmacy can
// have the medicine ready before they arrive and they collect it by token instead of queueing.
public class Prescription : AuditedEntity
{
    // Patient Management's row, id only - the same reference LabReport keeps.
    public Guid PatientId { get; set; }

    public string? Note { get; set; }

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public byte[] Content { get; set; } = null!;

    public int ByteSize { get; set; }

    public PrescriptionStatus Status { get; set; }

    // The collection token restarts at 1 every day, so the number alone is ambiguous; the pair is
    // what is unique.
    public DateOnly? TokenDate { get; set; }

    public int? TokenNumber { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public Guid? ReadyByStaffId { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public Guid? DeliveredByStaffId { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    public Guid? RejectedByStaffId { get; set; }
}
