using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class AllocationSummaryDto
{
    [Required]
    public Guid AllocationId { get; set; }

    [Required]
    public Guid ShiftId { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    public string EndTime { get; set; } = string.Empty;

    [Required]
    public Guid StaffMemberId { get; set; }

    [Required]
    public string StaffName { get; set; } = string.Empty;

    [Required]
    public AllocationStatus Status { get; set; }
}
