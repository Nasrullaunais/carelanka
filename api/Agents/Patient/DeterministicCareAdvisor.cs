using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The fallback drafter, used with no model configured, a dead key, an exhausted quota or a
/// timeout - and the one this validator always accepts. It never echoes the patient's own words
/// or a recorded allergy back into the message, so it cannot fail CR1 or CR5 by construction:
/// there is nothing free-text in it beyond the fixed sentences below.
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

        var message = hasHistory
            ? "Patient has raised a new concern during their stay. A recorded medical profile " +
              "exists for this patient - review it alongside the patient's own report below " +
              "before deciding what to do. Suggest a bedside review this shift."
            : "Patient has raised a new concern during their stay. No medical profile is on " +
              "record for this patient, so this draft is based on their own words alone. " +
              "Suggest a bedside review this shift.";

        // Marked as the backup by default. Callers that know the specific cause replace the note.
        return Task.FromResult(
            new CareDraftCandidate(urgency, message, CareDraftSource.ModelUnavailable));
    }
}
