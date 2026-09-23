using CareLanka.Api.Agents.Equipment;
using CareLanka.Api.Data.Enums;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// The fallback used with no model configured, a dead key, an exhausted quota, a timeout, or a
/// model answer RT1 rejected - so nothing about the agent's answer depends on a model being
/// reachable, exactly as <c>DeterministicCareAdvisor</c> guarantees for the care agent.
/// </summary>
public sealed class DeterministicReorderAdvisorTests
{
    private static readonly ReorderItemFacts Item = new("Amoxicillin 250mg", "tablet", 20, 100);

    [Fact]
    public async Task No_dispensing_in_the_window_keeps_the_current_threshold()
    {
        var history = new ReorderDispensingHistoryFacts(60, []);
        var advisor = new DeterministicReorderAdvisor(leadTimeDays: 7);

        var candidate = await advisor.SuggestAsync(new ReorderAdviceContext(Item, history));

        Assert.Equal(Item.CurrentThreshold, candidate.SuggestedThreshold);
        Assert.Equal(ReorderSuggestionSource.ModelUnavailable, candidate.Source);
        Assert.Contains("No", candidate.Reasoning);
    }

    [Fact]
    public async Task Suggests_average_daily_usage_times_the_lead_time_buffer_rounded_up()
    {
        // 30 units dispensed over a 10-day window = 3/day average; 7 days' buffer -> ceil(21) = 21.
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var daily = new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 }
            .Select((quantity, index) => new ReorderDailyDispensed(start.AddDays(index), quantity))
            .ToList();
        var history = new ReorderDispensingHistoryFacts(10, daily);
        var advisor = new DeterministicReorderAdvisor(leadTimeDays: 7);

        var candidate = await advisor.SuggestAsync(new ReorderAdviceContext(Item, history));

        Assert.Equal(21, candidate.SuggestedThreshold);
        Assert.Equal(ReorderSuggestionSource.ModelUnavailable, candidate.Source);
    }

    [Fact]
    public async Task Rounds_up_a_fractional_average_rather_than_truncating()
    {
        // 10 units over a 3-day window = 3.33/day average; 1 day's buffer -> ceil(3.33) = 4, not 3.
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var daily = new[]
        {
            new ReorderDailyDispensed(start, 4),
            new ReorderDailyDispensed(start.AddDays(1), 3),
            new ReorderDailyDispensed(start.AddDays(2), 3)
        };
        var history = new ReorderDispensingHistoryFacts(3, daily);
        var advisor = new DeterministicReorderAdvisor(leadTimeDays: 1);

        var candidate = await advisor.SuggestAsync(new ReorderAdviceContext(Item, history));

        Assert.Equal(4, candidate.SuggestedThreshold);
    }
}
