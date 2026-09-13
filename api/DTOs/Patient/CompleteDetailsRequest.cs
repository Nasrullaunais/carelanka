using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class CompleteDetailsRequest
{
    [MaxLength(20)]
    public string? Nic { get; set; }

    [MaxLength(200)]
    public string? FullName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactPhone { get; set; }
}
