using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class Patient : PatientSummary
{
    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    [Required]
    public bool HasAccount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
