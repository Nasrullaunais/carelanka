using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Staff;

public class Allocation : AuditedEntity
{
    public Guid ShiftId { get; set; }

    public Guid StaffMemberId { get; set; }

    public AllocationStatus Status { get; set; }

    public AllocationSource Source { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public AllocationEndReason? EndedReason { get; set; }

    /// <summary>
    /// Points to the allocation that replaced this one in a cascading swap.
    /// Null when this allocation was not part of a swap.
    /// </summary>
    public Guid? ReplacedByAllocationId { get; set; }

    public DateTimeOffset? ClockedInAt { get; set; }

    public DateTimeOffset? ClockedOutAt { get; set; }

    /// <summary>Staff member who manually created this allocation (null for agent-created).</summary>
    public Guid? CreatedByStaffId { get; set; }

    /// <summary>The roster proposal that produced this allocation. Null for manually created allocations.</summary>
    public Guid? RosterProposalId { get; set; }

    // Navigation
    public Shift Shift { get; set; } = null!;
    public Allocation? ReplacedByAllocation { get; set; }
}
