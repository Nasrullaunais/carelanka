using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One row on reception's worklist: a visit whose money has not been taken yet.
/// </summary>
/// <remarks>
/// Includes visits with no bill row at all, which is most of them - a bill is written the first
/// time somebody asks for one. A list of bills would therefore have shown reception an empty
/// screen and left the work invisible.
/// </remarks>
public class OutstandingBill
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public AdmissionStatus Status { get; set; }

    [Required]
    public AdmissionCategory AdmissionCategory { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    public DateTimeOffset? AdmittedAt { get; set; }

    /// <summary>Null when nobody has prepared a bill for this visit yet.</summary>
    public string? BillNumber { get; set; }

    /// <summary>
    /// What the bill comes to as it stands - the prepared total, or what preparing it now would
    /// produce. Advisory: the bill screen is what actually writes the lines.
    /// </summary>
    [Required]
    public decimal EstimatedTotal { get; set; }

    [Required]
    public string Currency { get; set; } = "LKR";
}
