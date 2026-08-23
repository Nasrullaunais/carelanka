namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// Who did what, written automatically by AuditSaveChangesInterceptor.
/// Never write one of these by hand.
/// PerformedByStaffMemberId is null for system-initiated changes, such as the
/// deterministic Warning generation in Equipment.
/// </summary>
public class AuditLog : Entity
{
    public required string EntityType { get; set; }
    public required Guid EntityId { get; set; }
    public required AuditOperation Operation { get; set; }
    public Guid? PerformedByStaffMemberId { get; set; }
}
