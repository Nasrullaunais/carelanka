using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One bed held or occupied for an admission. A row walks reserved → occupied → released, and
/// several rows per admission cover mid-stay transfers.
/// </summary>
public class BedAssignment
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public Guid BedId { get; set; }

    /// <summary>From Equipment Management's register. Empty while their bed lookup is stubbed — see STUBS.md row 1.</summary>
    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public AssignmentStatus Status { get; set; }

    /// <summary>The expiring hold. Past this instant the bed is free again, with no human action.</summary>
    public DateTimeOffset? ReservedUntil { get; set; }

    [Required]
    public AssignedBy AssignedBy { get; set; }

    public Guid? WorkflowId { get; set; }

    /// <summary>True when the bed is below the requested category. Always needs Duty Manager approval.</summary>
    [Required]
    public bool IsDowngrade { get; set; }

    public Guid? ApprovedByStaffId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? OverrideReason { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public ReleaseReason? ReleaseReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
