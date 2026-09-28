using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The allow-list as a type. Three read tools and nothing else - the write tool
/// (<c>draft_recommendation</c>) is not here because it is not a lookup: it is what the agent's
/// own service does with the model's answer, in <c>ICareRecommendationService</c>.
/// </summary>
public interface ICareAgentTools
{
    Task<CareMedicalProfileFacts?> GetMedicalProfileAsync(
        Guid patientId, CancellationToken ct = default);

    Task<CarePatientHistoryFacts> GetPatientHistoryAsync(
        Guid patientId, CancellationToken ct = default);

    Task<CareCurrentAdmissionFacts?> GetCurrentAdmissionAsync(
        Guid admissionId, CancellationToken ct = default);
}

public sealed record CareMedicalProfileFacts(
    string? KnownConditions, string? Allergies, string? CurrentSymptoms);

public sealed record CarePastAdmission(AdmissionCategory Category, AdmissionUrgency Urgency, DateTimeOffset? AdmittedAt);

/// <param name="ReplySent">
/// What the patient was actually told - the approved reply, never an unapproved draft. Without it
/// a follow-up like "what did you mean by that?" reads to the model as a message about nothing.
/// </param>
public sealed record CarePastRecommendation(
    string ReportedText, CareUrgency? UrgencyFlag, DateTimeOffset ReportedAt, string? ReplySent);

public sealed record CarePatientHistoryFacts(
    int? Age,
    Gender Gender,
    IReadOnlyList<CarePastAdmission> PastAdmissions,
    IReadOnlyList<CarePastRecommendation> PastRecommendations);

public sealed record CareCurrentAdmissionFacts(
    AdmissionCategory Category,
    AdmissionUrgency Urgency,
    bool IsInfectious,
    string? WardName,
    DateTimeOffset? AdmittedAt);
