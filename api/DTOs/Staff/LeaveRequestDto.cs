using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class LeaveRequestDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid StaffMemberId { get; set; }

    [Required]
    public string StaffName { get; set; } = string.Empty;

    [Required]
    public LeaveType Type { get; set; }

    [Required]
    public bool IsUrgent { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    [Required]
    public LeaveStatus Status { get; set; }

    public Guid? ReviewedByStaffId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    public Guid? SwapWithStaffMemberId { get; set; }

    public Guid? SwapShiftId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
