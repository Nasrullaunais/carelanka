using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

public class MaintenanceSchedule : AuditedEntity
{
    public AssetType AssetType { get; set; }

    public Guid AssetId { get; set; }

    public MaintenanceType ScheduleType { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public MaintenanceStatus Status { get; set; }

    public Guid? PerformedByStaffId { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Notes { get; set; }

    public RaisedBy CreatedBy { get; set; }
}
