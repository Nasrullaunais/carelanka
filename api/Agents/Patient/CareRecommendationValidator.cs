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
/// already on their record.
/// </para>
/// <para>
/// CR5 was narrowed again later the same day. Requiring a negative word in every sentence that
/// named a medicine threw away "Panadol is normally used for pain and fever." - a sentence the
/// prompt asks for, and one that points the patient nowhere - so in practice any question about a
/// medicine came back as the fallback note. CR5 now fails only a sentence that *directs* the
/// patient at the substance. CR1 is untouched and carries the weight: no dosages, and no medicine
/// the patient never raised.
/// </para>
/// <para>
/// <b>Both rules were wrong before they were right, and each wrong version passed its own tests
/// - it looked correct until a real model draft hit it.</b> If CR1 or CR5 needs to change again,
/// re-run the full <c>CareRecommendationValidatorTests</c> suite including the sentences each
/// narrowing exists to still catch ("You could ask the nurse for penicillin." must still fail),
/// not just the new case being added.
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
    /// What makes a sentence that tells the patient to take something acceptable after all -
    /// because it is telling them <em>not</em> to.
    /// </summary>
    /// <remarks>
    /// Stored already normalised. <see cref="Normalise"/> collapses doubled letters, so a sentence
    /// saying "allergy" arrives as <c>alergy</c>; a raw list here would never match those two and
    /// the marker would be dead.
    /// </remarks>
    private static readonly HashSet<string> NegativeMarkers = new(new[]
    {
        "not", "never", "avoid", "dont", "wont", "cant", "shouldnt", "mustnt", "unsafe", "stop",
        "allergic", "allergy", "reaction"
    }.Select(Normalise));

    /// <summary>
    /// Verbs that turn naming a medicine into directing the patient at it. Also normalised.
    /// </summary>
    private static readonly HashSet<string> DirectiveVerbs = new(new[]
    {
        "take", "taking", "use", "using", "try", "trying", "start", "swallow", "apply", "ask",
        "asking", "request"
    }.Select(Normalise));

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

        if (SomeSentenceDirectsThemToIt(message, named.Concat(allergens)))
        {
            failedRules.Add("CR5");
        }

        // CR4: the keyword screen outranks the model. A lower urgency here is overwritten rather
        // than trusted - the same instinct as the bed agent's hard rules.
        var urgency = redFlagMatched ? CareUrgency.High : candidate.UrgencyFlag;

        return new CareValidationResult(failedRules.Count == 0, failedRules, urgency);
    }

    /// <summary>
    /// Whether any sentence points the patient at one of <paramref name="substances"/> - tells them
    /// to take it, use it, or ask someone for it - without saying not to in that same sentence.
    /// Sentence by sentence rather than whole-message, so one "do not take" cannot license a
    /// recommendation three sentences later.
    /// </summary>
    /// <remarks>
    /// Narrowed on 2026-09-21. It used to require every sentence naming a medicine to carry a
    /// negative word, which failed "Panadol is normally used for pain and fever." - a sentence the
    /// prompt explicitly asks for, and one that directs the patient nowhere. The rule now tracks
    /// what it was always for: a draft must never tell a patient to take something. Describing a
    /// medicine is not directing them at it, and a nurse or doctor still reads every word.
    /// </remarks>
    private static bool SomeSentenceDirectsThemToIt(string message, IEnumerable<string> substances)
    {
        var wanted = substances
            .Select(Normalise)
            .Where(substance => substance.Length >= 3)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return false;
        }

        var sentences = message.Split(
            new[] { '.', '!', '?', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        return sentences
            .Select(Normalise)
            .Where(sentence => wanted.Any(sentence.Contains))
            .Any(sentence => Directs(sentence) && !IsNegative(sentence));
    }

    /// <summary>
    /// A directive is either an imperative - the sentence opens with the verb - or second person,
    /// "you can take", "you should ask". A passive description like "is normally used for" is
    /// neither, which is the whole point of reading it this way rather than hunting for the verb
    /// anywhere in the sentence.
    /// </summary>
    private static bool Directs(string normalisedSentence)
    {
        var words = normalisedSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return false;
        }

        var opening = words[0] == "please" && words.Length > 1 ? 1 : 0;

        if (DirectiveVerbs.Contains(words[opening]))
        {
            return true;
        }

        for (var index = 0; index < words.Length; index++)
        {
            if (words[index] != "you")
            {
                continue;
            }

            // "you can take", "you should ask the nurse for" - far enough to clear a modal and an
            // adverb, short enough that an unrelated verb later in the sentence is not caught.
            var end = Math.Min(words.Length, index + 5);

            for (var ahead = index + 1; ahead < end; ahead++)
            {
                if (DirectiveVerbs.Contains(words[ahead]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsNegative(string normalisedSentence)
    {
        var words = normalisedSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Any(NegativeMarkers.Contains);
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
