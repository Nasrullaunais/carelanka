using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class CreateLeaveRequest
{
    [Required]
    public LeaveType Type { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public Guid? SwapShiftId { get; set; }

    public Guid? SwapWithStaffMemberId { get; set; }
}
