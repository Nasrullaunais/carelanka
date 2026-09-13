using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The patient's own details, as they see them in the app. What joins a phone login to a
/// medical record.
/// </summary>
/// <remarks>
/// A separate and deliberately small shape, for the same reason <see cref="MyAdmission"/> is
/// one: it is not <see cref="Patient"/> with a filter over it. A filtered staff object leaks
/// the first time somebody adds a field and forgets the filter; a separate shape cannot leak
/// what it does not contain.
///
/// <b>No <c>id</c>.</b> Every patient-facing endpoint resolves the record from the token, so
/// the app never needs the internal key and publishing it only invites a client to send one
/// back. <c>patient_code</c> is here because it is on the wristband and staff ask for it out
/// loud, so the patient has to be able to read it off their own phone.
/// </remarks>
public class MyProfile
{
    /// <summary>The short handle staff ask for out loud: <c>P7K2X9QM</c>.</summary>
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

    /// <summary>Nothing left blank. The same calculation the ward clerk's screen reads.</summary>
    [Required]
    public bool DetailsComplete { get; set; }

    /// <summary>
    /// What is still blank, from the closed vocabulary of <c>PatientDetailField</c>, so the app
    /// can ask for exactly those and nothing else.
    /// </summary>
    [Required]
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
}
