using System.Text.Json.Serialization;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class CreatePatientRequest
{
    [JsonRequired]
    public string FullName { get; set; } = string.Empty;

    public string? Nic { get; set; }

    [JsonRequired]
    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }
}
