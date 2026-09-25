namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// A fixed keyword list, checked against the patient's own words before the model ever runs.
/// A match forces <c>red_flag = true</c> and <c>urgency_flag = high</c>, unconditionally - the
/// model can raise urgency further but can never lower a flag this screen already raised.
/// Deliberately data, not a model call: the one thing this agent must never get wrong is checked
/// in plain C#, same instinct as the bed agent's hard rules.
/// </summary>
public static class CareRedFlagScreen
{
    /// <summary>
    /// Editable without a code change in spirit - it is a plain list, not a resource that ships
    /// with the model. Kept here rather than in configuration because a red-flag keyword is a
    /// clinical-safety decision, not an environment setting.
    /// </summary>
    public static readonly IReadOnlyList<string> Keywords =
    [
        "chest pain",
        "heart attack",
        "cant breathe",
        "cannot breathe",
        "can not breathe",
        "not breathing",
        "short of breath",
        "shortness of breath",
        "difficulty breathing",
        "trouble breathing",
        "choking",
        "severe bleeding",
        "heavy bleeding",
        "coughing up blood",
        "vomiting blood",
        "loss of consciousness",
        "unconscious",
        "unresponsive",
        "passed out",
        "fainted",
        "stroke",
        "suicidal",
        "suicide",
        "kill myself",
        "cant move my",
        "cannot move my",
        "severe allergic reaction",
        "anaphylaxis",
        "seizure",
        "convulsion"
    ];

    /// <summary>
    /// Lower-cases, drops apostrophes of every kind and squeezes spaces, so a phone's curly
    /// apostrophe, a typed "can't" and a hurried "cant" all read the same. The keywords above
    /// are already stored in this form.
    /// </summary>
    public static string Normalise(string text)
    {
        var withoutApostrophes = text
            .ToLowerInvariant()
            .Replace("'", string.Empty)
            .Replace("\u2019", string.Empty)
            .Replace("\u2018", string.Empty)
            .Replace("\u02BC", string.Empty)
            .Replace("`", string.Empty);

        return string.Join(' ', withoutApostrophes.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool Matches(string reportedText)
    {
        if (string.IsNullOrWhiteSpace(reportedText))
        {
            return false;
        }

        var text = Normalise(reportedText);

        return Keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }
}
