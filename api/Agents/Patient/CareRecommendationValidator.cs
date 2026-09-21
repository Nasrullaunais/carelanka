using System.Text;
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
/// <para>
/// CR1 and CR5 were both widened on 2026-09-21. A blanket ban on naming any medicine meant the
/// safest possible sentence - "do not take penicillin, your record lists it as an allergy" - was
/// the one thing the agent could not say, and it answered a patient asking about their own
/// allergen with "something you react badly to". The line is no longer *whether* a medicine is
/// named, it is *how*: the agent may only ever name one the patient themselves raised or that is
/// already on their record, and only to be negative about it.
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
        "paracetamol iv", "diclofenac", "metronidazole", "azithromycin", "ciprofloxacin",
        // Brand names a Sri Lankan patient is far more likely to type than the generic.
        "panadol", "piriton", "disprin", "brufen", "amoxil", "zinnat", "voltaren"
    ];

    /// <summary>
    /// What makes a sentence naming a medicine acceptable. Whole words, matched against the
    /// normalised sentence, so "don't" and "shouldn't" arrive here as <c>dont</c> and
    /// <c>shouldnt</c>. A sentence that names a medicine and carries none of these reads as
    /// telling the patient to take it, which no draft is allowed to do.
    /// </summary>
    private static readonly string[] NegativeMarkers =
    [
        "not", "never", "avoid", "dont", "wont", "cant", "shouldnt", "mustnt", "unsafe", "stop",
        "allergic", "allergy", "reaction"
    ];

    [GeneratedRegex(@"\b\d+(\.\d+)?\s?(mg|mcg|ml|g|iu|units?|tablets?|tabs?|capsules?|caps?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DosagePattern();

    public static CareValidationResult Validate(
        CareDraftCandidate candidate,
        bool redFlagMatched,
        string? allergiesText,
        string? reportedText = null)
    {
        var failedRules = new List<string>();
        var message = candidate.Message ?? string.Empty;

        // Anything the patient raised themselves, plus anything already on their allergy record.
        // The agent may discuss those; it may not introduce a medicine of its own.
        var raised = Normalise(reportedText) + " " + Normalise(allergiesText);
        var written = Normalise(message);

        var named = DrugDenylist
            .Where(drug => written.Contains(Normalise(drug)))
            .ToList();

        if (DosagePattern().IsMatch(message) ||
            named.Any(drug => !raised.Contains(Normalise(drug))))
        {
            failedRules.Add("CR1");
        }

        var allergens = Substances(allergiesText);

        if (!EveryMentionIsNegative(message, named.Concat(allergens)))
        {
            failedRules.Add("CR5");
        }

        // CR4: the keyword screen outranks the model. A lower urgency here is overwritten rather
        // than trusted - the same instinct as the bed agent's hard rules.
        var urgency = redFlagMatched ? CareUrgency.High : candidate.UrgencyFlag;

        return new CareValidationResult(failedRules.Count == 0, failedRules, urgency);
    }

    /// <summary>
    /// Every sentence that names one of <paramref name="substances"/> has to be negative about it.
    /// Sentence by sentence rather than whole-message, so one "do not take" cannot license a
    /// recommendation three sentences later.
    /// </summary>
    private static bool EveryMentionIsNegative(string message, IEnumerable<string> substances)
    {
        var wanted = substances
            .Select(Normalise)
            .Where(substance => substance.Length >= 3)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return true;
        }

        var sentences = message.Split(
            new[] { '.', '!', '?', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        return sentences
            .Select(Normalise)
            .Where(sentence => wanted.Any(sentence.Contains))
            .All(IsNegative);
    }

    private static bool IsNegative(string normalisedSentence)
    {
        var words = normalisedSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Any(word => NegativeMarkers.Contains(word));
    }

    /// <summary>
    /// Splits the recorded allergies into individual substances. Only possible because the allergy
    /// is a stored field rather than a sentence in a note.
    /// </summary>
    public static IEnumerable<string> Substances(string? allergiesText)
        => string.IsNullOrWhiteSpace(allergiesText)
            ? []
            : allergiesText.Split(
                new[] { ',', ';', '/', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether <paramref name="text"/> names <paramref name="substance"/>, spelling allowed to be
    /// approximate. Shared with the deterministic drafter so both sides of the run agree on what
    /// counts as the patient having raised a substance.
    /// </summary>
    public static bool Names(string? text, string substance)
    {
        var wanted = Normalise(substance);

        return wanted.Length >= 3 && Normalise(text).Contains(wanted);
    }

    /// <summary>
    /// Lower-cased, apostrophes dropped, other punctuation turned into a space, and runs of one
    /// repeated letter collapsed - so the patient who typed "penicilin" and the record that says
    /// "Penicillin" both arrive here as the same word. Without that, the patient's own spelling
    /// decides whether a safety rule fires.
    /// </summary>
    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previous = '\0';

        foreach (var character in value.ToLowerInvariant())
        {
            if (character == '\'' || character == '’')
            {
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                if (previous != ' ')
                {
                    builder.Append(' ');
                }

                previous = ' ';
                continue;
            }

            if (character == previous)
            {
                continue;
            }

            builder.Append(character);
            previous = character;
        }

        return builder.ToString().Trim();
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
