namespace CareLanka.Api.Data.Entities.Equipment;

// Entity, not AuditedEntity: a report is issued once and never edited. A corrected result is a
// new row, so a ward can see that a correction happened rather than finding a value has quietly
// changed.
public class LabReport : Entity
{
    // Patient Management's row, id only. No name, no NIC, no ward, so there is no copy here to
    // go stale when they change one of theirs.
    public Guid PatientId { get; set; }

    public string TestName { get; set; } = null!;

    public string? Summary { get; set; }

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public byte[] Content { get; set; } = null!;

    public int ByteSize { get; set; }

    public Guid UploadedByStaffId { get; set; }
}
