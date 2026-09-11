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
