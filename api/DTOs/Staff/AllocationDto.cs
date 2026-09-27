using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class AllocationDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid ShiftId { get; set; }

    [Required]
    public Guid StaffMemberId { get; set; }

    [Required]
    public string StaffName { get; set; } = string.Empty;

    [Required]
    public AllocationStatus Status { get; set; }

    [Required]
    public AllocationSource Source { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public AllocationEndReason? EndedReason { get; set; }

    public Guid? ReplacedByAllocationId { get; set; }

    public DateTimeOffset? ClockedInAt { get; set; }

    public DateTimeOffset? ClockedOutAt { get; set; }

    public Guid? CreatedByStaffId { get; set; }

    public Guid? RosterProposalId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
