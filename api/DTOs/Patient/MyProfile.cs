using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class MyProfile
{
    [Required]
    [StringLength(8, MinimumLength = 8)]
    public string PatientCode { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    public string? Nic { get; set; }

    [Required]
    public Gender Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    [Required]
    public bool DetailsComplete { get; set; }

    [Required]
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
}
