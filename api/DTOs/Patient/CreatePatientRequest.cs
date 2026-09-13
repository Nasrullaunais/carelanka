using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.DTOs.Patient;

public class CreatePatientRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    [RegularExpression(
        PatientIdentifierFormats.Nic,
        ErrorMessage = PatientIdentifierFormats.NicMessage)]
    public string? Nic { get; set; }

    [Required]
    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    [DateOfBirth]
    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    [RegularExpression(
        PatientIdentifierFormats.Phone,
        ErrorMessage = PatientIdentifierFormats.PhoneMessage)]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    [RegularExpression(
        PatientIdentifierFormats.Phone,
        ErrorMessage = PatientIdentifierFormats.PhoneMessage)]
    public string? EmergencyContactPhone { get; set; }
}
