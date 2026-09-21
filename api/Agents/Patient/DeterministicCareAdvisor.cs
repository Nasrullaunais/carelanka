using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The fallback drafter, used with no model configured, a dead key, an exhausted quota or a
/// timeout - and the one this validator always accepts, because everything free-text in it is a
/// fixed sentence.
/// <para>
/// Addressed to the patient, like the model's own draft. It cannot answer their question - there
/// is no model here to read it - so it says what is true whatever they asked: someone is coming,
/// and medicines come from the nurse.
/// </para>
/// <para>
/// The one thing it does answer is the dangerous one. If the patient's own words name a substance
/// their record lists as an allergy, the warning is named and explicit, built from the stored
/// field rather than written by anything. That sentence being deterministic is the point: it is
/// the sentence that must not be a guess.
/// </para>
/// </summary>
public sealed class DeterministicCareAdvisor : ICareAdvisor
{
    public Task<CareDraftCandidate> AdviseAsync(
        CareAdviceContext context, CancellationToken ct = default)
    {
        var hasHistory = context.MedicalProfile is { } profile && (
            !string.IsNullOrWhiteSpace(profile.KnownConditions) ||
            !string.IsNullOrWhiteSpace(profile.Allergies) ||
            !string.IsNullOrWhiteSpace(profile.CurrentSymptoms) ||
            !string.IsNullOrWhiteSpace(profile.RecentSituation));

        var urgency = context.RedFlagMatched
            ? CareUrgency.High
            : hasHistory ? CareUrgency.Medium : CareUrgency.Low;

        var message = Warning(context) + (context.RedFlagMatched
            ? "Thank you for telling us. The ward staff have been told, and someone will come to " +
              "you as soon as they can. Please stay where you are, and press the call bell now if " +
              "you feel worse. Do not take anything that was not given to you here."
            : hasHistory
                ? "Thank you for telling us. A nurse or doctor will come and check on you this " +
                  "shift, and they will look at your record before they do. Please do not take " +
                  "anything that was not given to you here - ask your nurse first. Press the call " +
                  "bell if you feel worse before they arrive."
                : "Thank you for telling us. A nurse or doctor will come and check on you this " +
                  "shift. Please do not take anything that was not given to you here - ask your " +
                  "nurse first. Press the call bell if you feel worse before they arrive.");

        // Marked as the backup by default. Callers that know the specific cause replace the note.
        return Task.FromResult(
            new CareDraftCandidate(urgency, message, CareDraftSource.ModelUnavailable));
    }

    private static string Warning(CareAdviceContext context)
    {
        var named = CareRecommendationValidator
            .Substances(context.MedicalProfile?.Allergies)
            .FirstOrDefault(substance =>
                CareRecommendationValidator.Names(context.ReportedText, substance));

        return named is null
            ? string.Empty
            : $"Please do not take {named}. Your record lists it as an allergy, so it is not safe "
              + "for you and the ward will not give it to you. ";
    }
}
