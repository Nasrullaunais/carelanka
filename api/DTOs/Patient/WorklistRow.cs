using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class WorklistRow
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public WorklistKind Kind { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public WorklistStatus Status { get; set; }

    [Required]
    public bool RequiresBed { get; set; }

    public AdmissionSource? Source { get; set; }

    public AdmissionCategory? AdmissionCategory { get; set; }

    public AdmissionUrgency? Urgency { get; set; }

    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    [Required]
    public DateTimeOffset When { get; set; }

    public string? Reason { get; set; }
}
