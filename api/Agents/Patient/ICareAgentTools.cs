using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The allow-list as a type, same shape as <see cref="IBedAgentTools"/>. Three read tools and
/// nothing else - the write tool (<c>draft_recommendation</c>) is not here because it is not a
/// lookup: it is what the agent's own service does with the model's answer, in
/// <c>ICareRecommendationService</c>.
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
    string? KnownConditions, string? Allergies, string? CurrentSymptoms, string? RecentSituation);

public sealed record CarePastAdmission(AdmissionCategory Category, AdmissionUrgency Urgency, DateTimeOffset? AdmittedAt);

public sealed record CarePastRecommendation(string ReportedText, CareUrgency? UrgencyFlag, DateTimeOffset ReportedAt);

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
