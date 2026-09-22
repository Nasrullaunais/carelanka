using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Full staff-facing detail. NEVER returned to a patient - see <see cref="MyCareRecommendation"/>
/// for the narrow shape a patient actually receives.
/// </summary>
public sealed class CareRecommendation
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public Guid? AdmissionId { get; set; }

    public string ReportedText { get; set; } = string.Empty;

    public DateTimeOffset ReportedAt { get; set; }

    public bool RedFlag { get; set; }

    public CareUrgency? UrgencyFlag { get; set; }

    public string? AgentMessage { get; set; }

    public CareRecommendationStatus Status { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? ReviewedByStaffId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? DoctorMessage { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
