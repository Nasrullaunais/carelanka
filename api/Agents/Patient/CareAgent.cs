using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The Patient Care Advisory Agent. One run drafts one note for a nurse or doctor to check: screen
/// the patient's own words for a red flag, gather what the hospital already knows about them, ask
/// the model to combine the two, and re-check the result deterministically before anyone sees it.
/// <para>
/// It writes nothing to the domain beyond the draft itself - the <c>CareRecommendation</c> row is
/// created by the service before the agent ever runs, and this agent only ever fills in its draft
/// fields. Nothing reaches the patient until a human approves it.
/// </para>
/// </summary>
public sealed class CareAgent : ICareAgent
{
    /// <summary>
    /// Two retries, then a safe failure - same budget as the bed agent, for the same reason: a
    /// failure is a recorded outcome, never an exception thrown at the screen.
    /// </summary>
    public const int MaxAttempts = 3;

    public static readonly IReadOnlyList<string> Plan =
    [
        Screen,
        GatherProfile,
        GatherHistory,
        GatherAdmission,
        Draft,
        Validate,
        Pause
    ];

    private const string Screen = "screen_red_flags";
    private const string GatherProfile = "get_medical_profile";
    private const string GatherHistory = "get_patient_history";
    private const string GatherAdmission = "get_current_admission";
    private const string Draft = "draft_recommendation";
    private const string Validate = "validate_deterministically";
    private const string Pause = "pause_for_approval";

    private readonly ICareAgentTools _tools;
    private readonly ICareAdvisor _advisor;
    private readonly ILogger<CareAgent> _logger;

    public CareAgent(ICareAgentTools tools, ICareAdvisor advisor, ILogger<CareAgent> logger)
    {
        _tools = tools;
        _advisor = advisor;
        _logger = logger;
    }

    public async Task<CareAgentRun> RunAsync(CareAgentRequest request, CancellationToken ct = default)
    {
        var journal = new CareAgentJournal();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await AttemptAsync(request, journal, attempt, ct);
            }
            catch (ApiException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception failure)
            {
                journal.Fail(failure);

                if (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        failure, "Care agent attempt {Attempt} failed; retrying.", attempt);
                    continue;
                }

                _logger.LogError(
                    failure, "Care agent gave up after {Attempts} attempts.", MaxAttempts);

                return journal.SafeFailure(MaxAttempts);
            }
        }

        return journal.SafeFailure(MaxAttempts);
    }

    private async Task<CareAgentRun> AttemptAsync(
        CareAgentRequest request, CareAgentJournal journal, int attempt, CancellationToken ct)
    {
        var redFlag = journal.Step(Screen, () => CareRedFlagScreen.Matches(request.ReportedText));
        journal.RedFlag = redFlag;

        var profile = await journal.ToolAsync(
            GatherProfile, "get_medical_profile",
            () => _tools.GetMedicalProfileAsync(request.PatientId, ct));

        var history = await journal.ToolAsync(
            GatherHistory, "get_patient_history",
            () => _tools.GetPatientHistoryAsync(request.PatientId, ct));

        var admission = await journal.ToolAsync(
            GatherAdmission, "get_current_admission",
            () => _tools.GetCurrentAdmissionAsync(request.AdmissionId, ct));

        var context = new CareAdviceContext(request.ReportedText, redFlag, profile, admission, history);

        var candidate = await journal.StepAsync(Draft, () => _advisor.AdviseAsync(context, ct));

        var validated = journal.Step(
            Validate, () => CareRecommendationValidator.Validate(candidate, redFlag, profile?.Allergies));

        var final = candidate;

        if (!validated.Passed)
        {
            // A draft breaking CR1 or CR5 never reaches a reviewer. The deterministic fallback is
            // built from fixed sentences and structured facts only, so it always passes.
            _logger.LogWarning(
                "The care advisor's draft failed {FailedRules}; falling back to the deterministic draft.",
                string.Join(", ", validated.FailedRules));

            final = (await new DeterministicCareAdvisor().AdviseAsync(context, ct)) with
            {
                Source = CareDraftSource.ModelRejected,
                SourceNote =
                    "The AI model's draft broke a safety rule ("
                    + string.Join(", ", validated.FailedRules)
                    + ") and was thrown away, so the standard backup note was used instead."
            };

            validated = CareRecommendationValidator.Validate(final, redFlag, profile?.Allergies);
        }

        journal.Validation.Passed = validated.Passed;
        journal.Validation.FailedRules = validated.FailedRules;
        final = final with { UrgencyFlag = validated.UrgencyFlag };

        journal.Step(Pause, () => 0);

        var outcome = redFlag ? CareAgentOutcome.Escalated : CareAgentOutcome.Drafted;

        return journal.Finish(outcome, final, attempt);
    }
}

public interface ICareAgent
{
    Task<CareAgentRun> RunAsync(CareAgentRequest request, CancellationToken ct = default);
}

public sealed record CareAgentRequest(Guid PatientId, Guid AdmissionId, string ReportedText);
