using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class MyAdmission
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public AdmissionStatus Status { get; set; }

    [Required]
    public string StatusText { get; set; } = null!;

    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }

    public DateTimeOffset? DischargedAt { get; set; }

    public string? DischargeInstructions { get; set; }

    [Required]
    public bool DetailsComplete { get; set; }

    [Required]
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
}
