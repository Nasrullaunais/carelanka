using CareLanka.Api.Agents.Patient;
using CareLanka.Api.DTOs.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class CareAgentJournalTests
{
    /// <summary>
    /// The reviewer's progress list reads what has been saved so far. If the slow steps did not
    /// report as they finished, the list would sit on step one until the whole run was over.
    /// </summary>
    [Fact]
    public async Task Each_slow_step_reports_the_steps_so_far_as_it_finishes()
    {
        var reported = new List<string[]>();
        var journal = new CareAgentJournal(steps =>
        {
            reported.Add(steps.Select(step => step.Step).ToArray());
            return Task.CompletedTask;
        });

        journal.Step("screen_red_flags", () => false);
        await journal.ToolAsync("get_medical_profile", "get_medical_profile", () => Task.FromResult(1));
        await journal.StepAsync("draft_recommendation", () => Task.FromResult(2));

        Assert.Equal(2, reported.Count);
        Assert.Equal(new[] { "screen_red_flags", "get_medical_profile" }, reported[0]);
        Assert.Equal(
            new[] { "screen_red_flags", "get_medical_profile", "draft_recommendation" }, reported[1]);
    }

    [Fact]
    public async Task A_failed_step_is_not_reported_as_progress()
    {
        var calls = 0;
        var journal = new CareAgentJournal(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => journal.StepAsync<int>(
            "draft_recommendation", () => throw new InvalidOperationException("model down")));

        Assert.Equal(0, calls);
    }
}
