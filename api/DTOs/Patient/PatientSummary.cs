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
