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
    CarePatientHistoryFacts History,
    CareDraftRevision? Revision = null);

/// <summary>
/// A draft the validator threw away, handed back with what was wrong with it so the model can fix
/// that and keep the rest - rather than the patient getting the backup note for one bad sentence.
/// </summary>
public sealed record CareDraftRevision(string RejectedMessage, IReadOnlyList<string> Problems);
