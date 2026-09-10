using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/equipment-items/{id}/report-fault.</summary>
public class ReportFaultRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
