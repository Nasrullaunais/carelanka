using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Narrow patient-facing shape, same pattern as MyAdmission. No agent_message and no
/// rejection_reason - those fields do not exist here, so there is nothing to accidentally leak.
/// </summary>
public sealed class MyCareRecommendation
{
    public Guid Id { get; set; }

    public string ReportedText { get; set; } = string.Empty;

    public DateTimeOffset ReportedAt { get; set; }

    public CareRecommendationStatus Status { get; set; }

    /// <summary>
    /// Set only once <see cref="Status"/> is Approved.
    /// </summary>
    public string? DoctorMessage { get; set; }

    /// <summary>
    /// Who signed off the reply, and when. Set alongside <see cref="DoctorMessage"/>, so a
    /// patient never learns that a report they cannot read was reviewed, or by whom. Null name
    /// on an approved row means the reviewer's account has since been deactivated.
    /// </summary>
    public string? ReviewedByName { get; set; }

    public CareReviewerRole? ReviewedByRole { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
