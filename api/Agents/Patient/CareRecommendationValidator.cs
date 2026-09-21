using System.Text.RegularExpressions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Rules CR1-CR5, enforced in C# after the model drafts and before any reviewer sees the result.
/// A draft breaking CR1 or CR5 is rejected outright - the caller falls back to a safe deterministic
/// message rather than showing the reviewer something unsafe.
/// <para>
/// These matter more now the draft is written to the patient rather than about them: a reviewer
/// approving without reading publishes this text verbatim, so it has to be safe before they see it.
/// </para>
/// </summary>
public static partial class CareRecommendationValidator
{
    private static readonly string[] DrugDenylist =
    [
        "paracetamol", "acetaminophen", "ibuprofen", "aspirin", "amoxicillin", "penicillin",
        "morphine", "pethidine", "tramadol", "codeine", "insulin", "metformin", "warfarin",
        "omeprazole", "diazepam", "amoxyclav", "augmentin", "ceftriaxone", "atropine",
        "adrenaline", "epinephrine", "salbutamol", "prednisolone", "furosemide", "heparin",
        "paracetamol iv", "diclofenac", "metronidazole", "azithromycin", "ciprofloxacin"
    ];

    [GeneratedRegex(@"\b\d+(\.\d+)?\s?(mg|mcg|ml|g|iu|units?|tablets?|tabs?|capsules?|caps?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DosagePattern();

    public static CareValidationResult Validate(
        CareDraftCandidate candidate, bool redFlagMatched, string? allergiesText)
    {
        var failedRules = new List<string>();
        var message = candidate.Message ?? string.Empty;

        if (ContainsDrugOrDosage(message))
        {
            failedRules.Add("CR1");
        }

        if (ContradictsAllergies(message, allergiesText))
        {
            failedRules.Add("CR5");
        }

        // CR4: the keyword screen outranks the model. A lower urgency here is overwritten rather
        // than trusted - the same instinct as the bed agent's hard rules.
        var urgency = redFlagMatched ? CareUrgency.High : candidate.UrgencyFlag;

        return new CareValidationResult(failedRules.Count == 0, failedRules, urgency);
    }

    private static bool ContainsDrugOrDosage(string message)
    {
        if (DosagePattern().IsMatch(message))
        {
            return true;
        }

        return DrugDenylist.Any(drug =>
            message.Contains(drug, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Splits the recorded allergies into individual substances and checks the draft never names
    /// one of them. Only possible because the allergy is a stored field, not a sentence in a note.
    /// </summary>
    private static bool ContradictsAllergies(string message, string? allergiesText)
    {
        if (string.IsNullOrWhiteSpace(allergiesText))
        {
            return false;
        }

        var substances = allergiesText
            .Split(new[] { ',', ';', '/', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3);

        return substances.Any(substance =>
            message.Contains(substance, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// <paramref name="Source"/> is what tells a reviewer whether they are reading the model's own
/// reasoning about this patient or the fixed backup sentence. The two are indistinguishable by
/// eye, and approving the second one is a waste of the reviewer's time.
/// </summary>
public sealed record CareDraftCandidate(
    CareUrgency UrgencyFlag,
    string Message,
    CareDraftSource Source = CareDraftSource.Model,
    string? SourceNote = null);

public sealed record CareValidationResult(
    bool Passed, IReadOnlyList<string> FailedRules, CareUrgency UrgencyFlag);
