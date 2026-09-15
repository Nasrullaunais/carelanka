using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Staff;

public class LeaveRequest : AuditedEntity
{
    public Guid StaffMemberId { get; set; }

    public LeaveType Type { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; }

    public Guid? ReviewedByStaffMemberId { get; set; }

    public string? ReviewNotes { get; set; }

    /// <summary>Only populated when Type = ShiftSwap: the colleague swapping in.</summary>
    public Guid? SwapWithStaffMemberId { get; set; }

    /// <summary>Only populated when Type = ShiftSwap: the shift being swapped.</summary>
    public Guid? SwapShiftId { get; set; }

    // Navigation
    public Shift? SwapShift { get; set; }
}
