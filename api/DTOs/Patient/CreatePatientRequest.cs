using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of POST /api/patients. Registration is done BY staff ABOUT a person — the patient does
/// not sign up for it and may never have an account.
/// </summary>
/// <remarks>
/// Minimum viable identity is a name plus an NIC or a phone. With neither, the server generates
/// a temp_reference, because an unconscious arrival with no documents still has to be
/// identifiable to the ward.
/// </remarks>
public class CreatePatientRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Omit for an unidentified arrival; the server then generates a temp_reference.</summary>
    [MaxLength(20)]
    public string? Nic { get; set; }

    [Required]
    [EnumDataType(typeof(Gender))]
    public Gender Gender { get; set; }

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
