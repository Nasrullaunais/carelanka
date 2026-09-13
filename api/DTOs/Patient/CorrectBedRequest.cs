using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class CorrectBedRequest
{
    [Required]
    public Guid? BedId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}
