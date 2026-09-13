using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Bill : AuditedEntity
{
    public Guid AdmissionId { get; set; }

    public Admission Admission { get; set; } = null!;

    public string BillNumber { get; set; } = null!;

    public Guid? RaisedByStaffMemberId { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid? SettledByStaffMemberId { get; set; }

    public string? SettlementNote { get; set; }

    public ICollection<BillLineItem> LineItems { get; set; } = new List<BillLineItem>();

    public bool IsSettled => SettledAt is not null;
}
