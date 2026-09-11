using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The discharge record for one admission - its checklist, and who signed it off.
/// </summary>
/// <remarks>
/// The row is created the first time anybody touches the checklist, not at admission time.
/// That way every visit already on the system when billing landed has one the moment it is
/// needed, and no backfill was required.
/// </remarks>
public class Discharge
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public AssignedBy FlaggedBy { get; set; }

    [Required]
    public DateTimeOffset FlaggedAt { get; set; }

    /// <summary>Keyed by the item name - <c>clinical_clearance</c>, <c>billing_settled</c> and so on.</summary>
    [Required]
    public IDictionary<string, ChecklistItem> Checklist { get; set; }
        = new Dictionary<string, ChecklistItem>();

    /// <summary>What the candidate list is a query for, and what confirming a discharge needs.</summary>
    [Required]
    public bool AllMandatoryTicked { get; set; }

    public Guid? ConfirmedByStaffId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public string? SummaryNote { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
