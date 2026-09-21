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
        "can't breathe",
        "cannot breathe",
        "can not breathe",
        "severe bleeding",
        "heavy bleeding",
        "loss of consciousness",
        "unconscious",
        "unresponsive",
        "stroke",
        "suicidal",
        "suicide",
        "can't move my",
        "cannot move my",
        "severe allergic reaction",
        "anaphylaxis",
        "seizure",
        "convulsion"
    ];

    public static bool Matches(string reportedText)
    {
        if (string.IsNullOrWhiteSpace(reportedText))
        {
            return false;
        }

        return Keywords.Any(keyword =>
            reportedText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
