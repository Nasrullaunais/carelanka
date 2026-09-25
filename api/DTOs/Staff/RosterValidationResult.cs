using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class RosterValidationResult
{
    [Required]
    public string Check { get; set; } = string.Empty;

    [Required]
    public bool Passed { get; set; }

    [Required]
    public string Detail { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}