using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class ReplaceWardStaffingRulesResponse
{
    [Required]
    public List<WardStaffingRuleDto> Rules { get; set; } = new();

    [Required]
    public List<ShiftSummaryDto> ShiftsNowDisagreeing { get; set; } = new();
}
