using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class BedAssignment
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public Guid BedId { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public AssignmentStatus Status { get; set; }

    public DateTimeOffset? ReservedUntil { get; set; }

    [Required]
    public bool IsDowngrade { get; set; }

    public Guid? ApprovedByStaffId { get; set; }

    public string? ApprovedByStaffName { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? OverrideReason { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public ReleaseReason? ReleaseReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
