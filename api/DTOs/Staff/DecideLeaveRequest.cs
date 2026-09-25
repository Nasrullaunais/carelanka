using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class DecideLeaveRequest
{
    [Required]
    public string Decision { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
