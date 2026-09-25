namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The one seam a language model plugs into for this agent. Given everything the tools gathered,
/// produce a draft urgency flag and message - or nothing, if no model is configured or the call
/// fails. Every caller has <see cref="DeterministicCareAdvisor"/> to fall back to, so nothing
/// about the answer depends on a model being reachable.
/// </summary>
public interface ICareAdvisor
{
    Task<CareDraftCandidate> AdviseAsync(CareAdviceContext context, CancellationToken ct = default);
}

public sealed record CareAdviceContext(
    string ReportedText,
    bool RedFlagMatched,
    CareMedicalProfileFacts? MedicalProfile,
    CareCurrentAdmissionFacts? CurrentAdmission,
    CarePatientHistoryFacts History);
