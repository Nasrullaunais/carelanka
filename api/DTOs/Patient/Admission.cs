using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class Admission : AdmissionSummary
{
    public string? DispatchId { get; set; }

    [Required]
    public Guid CategorySetByStaffId { get; set; }

    public string? CategorySetByStaffName { get; set; }

    [Required]
    public DateTimeOffset CategorySetAt { get; set; }

    [Required]
    public bool IsInfectious { get; set; }

    public Guid? ReportedByUserId { get; set; }

    [Required]
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();

    public DateTimeOffset? DischargedAt { get; set; }

    public CancelReason? CancelReason { get; set; }

    public string? CancelNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
