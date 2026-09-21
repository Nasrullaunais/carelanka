using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

/// <summary>
/// One row per symptom or concern a patient raises while admitted. The Patient Care Advisory
/// Agent drafts <see cref="AgentMessage"/> for clinical staff; nothing here reaches the patient
/// until a Doctor or Ward Nurse approves it and writes <see cref="DoctorMessage"/>.
/// </summary>
public class CareRecommendation : AuditedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid? AdmissionId { get; set; }

    public Admission? Admission { get; set; }

    public string ReportedText { get; set; } = string.Empty;

    public DateTimeOffset ReportedAt { get; set; }

    public bool RedFlag { get; set; }

    public CareUrgency? UrgencyFlag { get; set; }

    /// <summary>
    /// The agent's draft. Staff-facing only - never returned on any patient-facing route.
    /// </summary>
    public string? AgentMessage { get; set; }

    public CareRecommendationStatus Status { get; set; } = CareRecommendationStatus.PendingReview;

    public Guid? ReviewedByStaffMemberId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// What the patient actually sees. Set only at approval time - CR3 keeps this null until
    /// <see cref="Status"/> is <see cref="CareRecommendationStatus.Approved"/>.
    /// </summary>
    public string? DoctorMessage { get; set; }

    /// <summary>
    /// Staff-facing only. Never sent to the patient.
    /// </summary>
    public string? RejectionReason { get; set; }
}
