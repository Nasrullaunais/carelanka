using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class AssignBedRequest
{
    [Required]
    public Guid? BedId { get; set; }

    [MaxLength(500)]
    public string? OverrideReason { get; set; }
}
