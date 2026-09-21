using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The fallback drafter, used with no model configured, a dead key, an exhausted quota or a
/// timeout - and the one this validator always accepts. It never echoes the patient's own words
/// or a recorded allergy back into the message, so it cannot fail CR1 or CR5 by construction:
/// there is nothing free-text in it beyond the fixed sentences below.
/// <para>
/// Addressed to the patient, like the model's own draft. It cannot answer their question - it
/// has not read it - so it says what is true whatever they asked: someone is coming, and
/// medicines come from the nurse.
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

        var message = context.RedFlagMatched
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
                  "nurse first. Press the call bell if you feel worse before they arrive.";

        // Marked as the backup by default. Callers that know the specific cause replace the note.
        return Task.FromResult(
            new CareDraftCandidate(urgency, message, CareDraftSource.ModelUnavailable));
    }
}
