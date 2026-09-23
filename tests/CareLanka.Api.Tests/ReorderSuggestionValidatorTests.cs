using CareLanka.Api.Agents.Equipment;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// RT1 is the safety net between whatever the model wrote and a reviewer's screen, so it is
/// tested directly against the deterministic validator rather than through the model - same
/// reasoning as <see cref="CareRecommendationValidatorTests"/>.
/// </summary>
public sealed class ReorderSuggestionValidatorTests
{
    private static readonly ReorderItemFacts Item = new("Amoxicillin 250mg", "tablet", 20, 100);

    [Fact]
    public void A_reasonable_suggestion_passes()
    {
        var history = History(10, 12, 8);
        var candidate = new ReorderDraftCandidate(30, "Based on recent usage.");

        var result = ReorderSuggestionValidator.Validate(candidate, Item, history);

        Assert.True(result.Passed);
        Assert.Null(result.FailedRule);
    }

    [Fact]
    public void A_negative_suggestion_fails_RT1()
    {
        var result = ReorderSuggestionValidator.Validate(
            new ReorderDraftCandidate(-1, "Not possible."), Item, History());

        Assert.False(result.Passed);
        Assert.Equal("RT1", result.FailedRule);
    }

    [Fact]
    public void A_suggestion_wildly_above_both_the_threshold_and_peak_usage_fails_RT1()
    {
        // 5x the current threshold (20) is 100, and 14x the peak day (12) is 168 - well past both.
        var result = ReorderSuggestionValidator.Validate(
            new ReorderDraftCandidate(5000, "The model invented a number."), Item, History(10, 12, 8));

        Assert.False(result.Passed);
        Assert.Equal("RT1", result.FailedRule);
    }

    [Fact]
    public void A_new_item_with_no_threshold_and_no_history_still_gets_the_minimum_cap()
    {
        var newItem = new ReorderItemFacts("Brand New Drug", "box", 0, 0);

        var result = ReorderSuggestionValidator.Validate(
            new ReorderDraftCandidate(20, "No history yet."), newItem, History());

        Assert.True(result.Passed);
    }

    private static ReorderDispensingHistoryFacts History(params int[] dailyQuantities)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-dailyQuantities.Length);

        var daily = dailyQuantities
            .Select((quantity, index) => new ReorderDailyDispensed(start.AddDays(index), quantity))
            .ToList();

        return new ReorderDispensingHistoryFacts(60, daily);
    }
}
