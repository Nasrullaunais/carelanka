namespace CareLanka.Api.DTOs.Patient;

public class CompleteDetailsRequest
{
    public string? Nic { get; set; }

    public string? FullName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }
}
