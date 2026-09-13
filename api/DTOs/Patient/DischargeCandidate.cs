using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class DischargeCandidate
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public AdmissionCategory AdmissionCategory { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }

    [Required]
    public int DaysInBed { get; set; }

    [Required]
    public IReadOnlyList<string> OutstandingItems { get; set; } = Array.Empty<string>();

    [Required]
    public bool IsDischarged { get; set; }

    public DateTimeOffset? DischargedAt { get; set; }
}
