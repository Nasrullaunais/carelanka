using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The short view of a patient — enough to identify one row in a list or a lookup result,
/// and nothing more. Contact details and the account link are on <see cref="Patient"/>.
/// </summary>
public class PatientSummary
{
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// The short handle staff use out loud and type into a form: <c>P7K2X9QM</c>. Eight
    /// characters, generated once at registration and never changed. Other components identify
    /// a patient by this; <c>id</c> stays the key every stored reference uses.
    /// </summary>
    [Required]
    // Always exactly eight, both ways. The length is published so a form field can cap itself
    // at the right number rather than guessing from an example.
    [StringLength(8, MinimumLength = 8)]
    public string PatientCode { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    /// <summary>Null for an unidentified arrival; that row carries a TempReference instead.</summary>
    public string? Nic { get; set; }

    /// <summary>Present only for a patient registered with no NIC and no phone, e.g. UNKNOWN-2026-0142.</summary>
    public string? TempReference { get; set; }

    [Required]
    public Gender Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
