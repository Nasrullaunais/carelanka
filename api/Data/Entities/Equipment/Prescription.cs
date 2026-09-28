using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// Two ways one of these is born: a patient photographs a paper prescription and sends it from
// the app (FileName/ContentType/Content set, Body null), or a doctor types one directly for a
// patient they looked up (Body set, no file). Exactly one of the two is ever present.
public class Prescription : AuditedEntity
{
    // Patient Management's row, id only - the same reference LabReport keeps.
    public Guid PatientId { get; set; }

    public string? Note { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public byte[]? Content { get; set; }

    public int? ByteSize { get; set; }

    /// <summary>What a doctor typed directly, in place of a photographed file.</summary>
    public string? Body { get; set; }

    /// <summary>Set only when a doctor wrote this one directly; null for a patient's own upload.</summary>
    public Guid? PrescribedByStaffId { get; set; }

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
