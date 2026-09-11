using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;

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
    /// <remarks>
    /// A Sri Lankan NIC in either form, or a passport number for a foreign patient. Optional,
    /// and validated only when it is filled in - see <see cref="PatientIdentifierFormats"/>.
    /// </remarks>
    [MaxLength(20)]
    [RegularExpression(
        PatientIdentifierFormats.Nic,
        ErrorMessage = PatientIdentifierFormats.NicMessage)]
    public string? Nic { get; set; }

    /// <summary>Drives hard rule H3, the ward gender-policy filter.</summary>
    /// <remarks>
    /// Nullable, which looks like a mistake and is not. [Required] on a plain enum always passes,
    /// because the model binder has already turned an absent key into the first declared member
    /// - here `male`. That would defeat something deliberate: `Gender.Unknown` exists precisely
    /// so H3 behaves deterministically for an unidentified arrival, and its own comment says so.
    /// Recording that patient as male is exactly the case `Unknown` was added to handle, so the
    /// key has to be a 400 when missing rather than a guess.
    /// </remarks>
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

    /// <summary>The name of the person to ring, not the relationship and not the number.</summary>
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    [RegularExpression(
        PatientIdentifierFormats.Phone,
        ErrorMessage = PatientIdentifierFormats.PhoneMessage)]
    public string? EmergencyContactPhone { get; set; }
}
