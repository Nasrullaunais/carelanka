namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// Rule RT1, enforced in C# after the model drafts and before any reviewer sees the result. A
/// draft breaking it is rejected outright - the caller falls back to the deterministic formula
/// rather than showing the reviewer a number the model invented out of thin air.
/// </summary>
public static class ReorderSuggestionValidator
{
    /// <summary>How far above the current threshold a suggestion is still trusted.</summary>
    private const int ThresholdMultiplier = 5;

    /// <summary>How many peak-usage days' worth is still trusted, for an item with a very low or
    /// zero current threshold.</summary>
    private const int PeakDayMultiplier = 14;

    /// <summary>A floor under both of the above, so a brand-new item with no threshold and no
    /// history yet isn't capped at zero.</summary>
    private const int MinimumCap = 20;

    public static ReorderValidationResult Validate(
        ReorderDraftCandidate candidate, ReorderItemFacts item, ReorderDispensingHistoryFacts history)
    {
        if (candidate.SuggestedThreshold < 0)
        {
            return new ReorderValidationResult(false, "RT1");
        }

        var peakDaily = history.DailyDispensed.Count == 0
            ? 0
            : history.DailyDispensed.Max(day => day.Quantity);

        var cap = new[]
        {
            item.CurrentThreshold * ThresholdMultiplier,
            peakDaily * PeakDayMultiplier,
            MinimumCap
        }.Max();

        return candidate.SuggestedThreshold > cap
            ? new ReorderValidationResult(false, "RT1")
            : new ReorderValidationResult(true, null);
    }
}

public sealed record ReorderValidationResult(bool Passed, string? FailedRule);
