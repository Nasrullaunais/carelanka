using CareLanka.Api.Agents.Equipment;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// The orchestration around the model, not the model itself: the fixed plan, the retry/safe-
/// failure budget, RT1 catching a bad draft, and the fact the agent has no tool to apply its own
/// suggestion - same shape as <c>DispatchAgentTests</c>, tested with fakes rather than Gemini.
/// </summary>
public sealed class ReorderAgentTests
{
    private static readonly ReorderItemFacts DefaultItem = new("Amoxicillin 250mg", "tablet", 20, 100);

    [Fact]
    public async Task A_normal_run_suggests_a_threshold_and_follows_the_full_plan_in_order()
    {
        var advisor = new FixedAdvisor(new ReorderDraftCandidate(30, "Steady recent usage."));
        var tools = new FakeTools();

        var run = await Agent(tools, advisor).RunAsync(Request());

        Assert.Equal(ReorderAgentOutcome.Suggested, run.Outcome);
        Assert.Equal(30, run.Draft!.SuggestedThreshold);
        Assert.True(run.ValidationPassed);
        Assert.Equal(ReorderAgent.Plan, run.Steps.Select(step => step.Step));
        Assert.All(run.Steps, step => Assert.True(step.Ok));
    }

    [Fact]
    public async Task A_model_draft_that_breaks_RT1_is_discarded_for_the_deterministic_fallback()
    {
        var tools = new FakeTools();
        var advisor = new FixedAdvisor(new ReorderDraftCandidate(999_999, "The model invented a number."));

        var run = await Agent(tools, advisor).RunAsync(Request());

        Assert.Equal(ReorderAgentOutcome.Suggested, run.Outcome);
        Assert.True(run.ValidationPassed);
        Assert.Null(run.FailedRule);
        Assert.Equal(ReorderSuggestionSource.ModelRejected, run.Draft!.Source);
        Assert.NotEqual(999_999, run.Draft.SuggestedThreshold);
    }

    /// <summary>
    /// Approval enforcement, proven structurally rather than by exercising a code path: this
    /// agent is physically unable to write a threshold, because <see cref="IReorderAgentTools"/>
    /// has no method that could. Applying a suggestion stays a separate, deliberate action a human
    /// takes through the plain item-edit endpoint.
    /// </summary>
    [Fact]
    public void The_tool_surface_has_no_way_to_write_the_threshold_itself()
    {
        var methodNames = typeof(IReorderAgentTools).GetMethods().Select(method => method.Name);

        Assert.All(methodNames, name => Assert.StartsWith("Get", name));
    }

    [Fact]
    public async Task A_transient_tool_failure_is_retried_and_the_run_still_completes()
    {
        var tools = new FakeTools { FailFirstAttempts = 1 };
        var advisor = new FixedAdvisor(new ReorderDraftCandidate(10, "Fine."));

        var run = await Agent(tools, advisor).RunAsync(Request());

        Assert.Equal(ReorderAgentOutcome.Suggested, run.Outcome);
        Assert.Equal(2, run.Attempts);
        Assert.Single(run.Errors);
    }

    [Fact]
    public async Task Exhausting_every_attempt_ends_in_a_safe_failure_not_an_exception()
    {
        var tools = new FakeTools { FailFirstAttempts = int.MaxValue };
        var advisor = new FixedAdvisor(new ReorderDraftCandidate(10, "Never reached."));

        var run = await Agent(tools, advisor).RunAsync(Request());

        Assert.Equal(ReorderAgentOutcome.Failed, run.Outcome);
        Assert.Null(run.Draft);
        Assert.False(run.ValidationPassed);
        Assert.Equal(ReorderAgent.MaxAttempts, run.Attempts);
        Assert.Equal(ReorderAgent.MaxAttempts, run.Errors.Count);
    }

    [Fact]
    public async Task An_item_that_does_not_exist_fails_fast_without_burning_a_retry_budget()
    {
        var tools = new FakeTools { Item = null };

        await Assert.ThrowsAsync<NotFoundException>(() => Agent(tools).RunAsync(Request()));

        Assert.Equal(1, tools.ItemCalls);
    }

    private static ReorderAgent Agent(IReorderAgentTools tools, IReorderAdvisor? advisor = null)
        => new(
            tools,
            advisor ?? new DeterministicReorderAdvisor(),
            Options.Create(new EquipmentOptions()),
            NullLogger<ReorderAgent>.Instance);

    private static ReorderAgentRequest Request() => new(Guid.NewGuid());

    private sealed class FixedAdvisor(ReorderDraftCandidate candidate) : IReorderAdvisor
    {
        public Task<ReorderDraftCandidate> SuggestAsync(ReorderAdviceContext context, CancellationToken ct = default)
            => Task.FromResult(candidate);
    }

    private sealed class FakeTools : IReorderAgentTools
    {
        public int ItemCalls { get; private set; }

        public ReorderItemFacts? Item { get; init; } = DefaultItem;

        public ReorderDispensingHistoryFacts History { get; init; } = new(60, []);

        /// <summary>How many of the earliest calls to <see cref="GetItemAsync"/> throw before one succeeds.</summary>
        public int FailFirstAttempts { get; init; }

        public Task<ReorderItemFacts?> GetItemAsync(Guid pharmacyItemId, CancellationToken ct = default)
        {
            ItemCalls++;

            if (ItemCalls <= FailFirstAttempts)
            {
                throw new InvalidOperationException("Simulated transient failure.");
            }

            return Task.FromResult(Item);
        }

        public Task<ReorderDispensingHistoryFacts> GetDispensingHistoryAsync(
            Guid pharmacyItemId, int days, CancellationToken ct = default)
            => Task.FromResult(History);
    }
}
