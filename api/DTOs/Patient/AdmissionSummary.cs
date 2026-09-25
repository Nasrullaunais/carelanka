using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class AdmissionSummary
{
    [Required]
    public Guid Id { get; set; }

    public PatientSummary? Patient { get; set; }

    [Required]
    public AdmissionSource Source { get; set; }

    public AdmissionCategory? AdmissionCategory { get; set; }

    [Required]
    public AdmissionUrgency Urgency { get; set; }

    [Required]
    public AdmissionStatus Status { get; set; }

    [Required]
    public bool DetailsComplete { get; set; }

    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }
}
