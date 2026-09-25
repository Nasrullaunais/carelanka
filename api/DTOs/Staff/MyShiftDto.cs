using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class MyShiftDto
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
    public bool CrossesMidnight { get; set; }

    [Required]
    public AllocationStatus Status { get; set; }

    public DateTimeOffset? ClockedInAt { get; set; }

    public DateTimeOffset? ClockedOutAt { get; set; }

    [Required]
    public bool CanClockIn { get; set; }

    [Required]
    public bool WasReassigned { get; set; }
}
