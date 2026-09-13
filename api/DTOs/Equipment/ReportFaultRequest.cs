using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class ReportFaultRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
