using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class ShiftCoverageDto
{
    [Required]
    public int ConfirmedCount { get; set; }

    [Required]
    public int HeadcountNeeded { get; set; }

    [Required]
    public int MinimumHeadcount { get; set; }

    [Required]
    public CoverageStatus Status { get; set; }

    [Required]
    public int ShortfallToMinimum { get; set; }
}
