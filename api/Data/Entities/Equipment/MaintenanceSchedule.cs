using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// One row per maintenance, calibration or repair event, for either an equipment item or a
// bed. The reference is polymorphic rather than two nullable foreign keys, so the scheduling
// flow is written once.
public class MaintenanceSchedule : AuditedEntity
{
    public AssetType AssetType { get; set; }

    public Guid AssetId { get; set; }

    public MaintenanceType ScheduleType { get; set; }

    public DateOnly ScheduledDate { get; set; }

    // Never holds Overdue. That is derived when the row is read.
    public MaintenanceStatus Status { get; set; }

    public Guid? PerformedByStaffId { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Notes { get; set; }

    public RaisedBy CreatedBy { get; set; }
}
