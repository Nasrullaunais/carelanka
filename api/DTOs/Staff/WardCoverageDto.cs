using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class WardCoverageDto
{
    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    public Guid? CurrentShiftId { get; set; }

    [Required]
    public int OnDutyCount { get; set; }

    [Required]
    public int MinimumHeadcount { get; set; }

    [Required]
    public int HeadcountNeeded { get; set; }

    [Required]
    public CoverageStatus Status { get; set; }

    [Required]
    public Dictionary<string, int> ByRole { get; set; } = new();
}
