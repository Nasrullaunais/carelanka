namespace CareLanka.Api.Data.Entities.Equipment;

// A finished laboratory result, filed against the patient it belongs to. The point of the row
// is that a ward can read a result the moment the lab issues it, instead of waiting for paper
// to be carried up from the basement.
//
// Entity rather than AuditedEntity: a report is issued once and never edited. A corrected
// result is a new report, the same way a replacement machine is a new EquipmentItem - so the
// ward can see that a correction happened rather than finding a value has quietly changed.
//
// PatientId is a reference into Patient Management's table and nothing more. No name, no NIC,
// no ward: exactly the rule EquipmentItem.AssignedToAdmissionId already follows, so a patient
// renamed or discharged there does not leave a stale copy here.
public class LabReport : Entity
{
    public Guid PatientId { get; set; }

    /// <summary>What was tested, in the lab's own words. "Full blood count", "Serum creatinine".</summary>
    public string TestName { get; set; } = null!;

    /// <summary>The lab's short summary, if they wrote one. Never a substitute for the file.</summary>
    public string? Summary { get; set; }

    public string FileName { get; set; } = null!;

    /// <summary>Needed to serve the file back as what it is, so a browser opens a PDF rather than downloading bytes.</summary>
    public string ContentType { get; set; } = null!;

    /// <summary>Stored in the database, so a report cannot go missing from a folder nobody backed up.</summary>
    public byte[] Content { get; set; } = null!;

    /// <summary>Published in the list so a ward sees the size before opening it on ward wifi.</summary>
    public int ByteSize { get; set; }

    // Staff Management owns the person; we store the id and nothing else. Plan section 13.3.
    public Guid UploadedByStaffId { get; set; }
}
