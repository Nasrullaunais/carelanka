using CareLanka.Api.Agents;
using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class DispatchAdvisorTests
{
    private static readonly RankedAmbulance Fast = new(Guid.NewGuid(), "WP-CAL-101", 6, 2, 20);
    private static readonly RankedAmbulance Fuller = new(Guid.NewGuid(), "WP-CAL-102", 8, 3, 15);
    private static readonly RankedAmbulance Slow = new(Guid.NewGuid(), "WP-CAL-103", 14, 3, 10);

    private static readonly DispatchChoiceContext Context = new(
        new DispatchCallFacts(CallPriority.Critical, "Chest pain, not breathing well", PatientKnown: true),
        [Fast, Fuller, Slow]);

    [Fact]
    public async Task A_recorded_model_answer_is_used_as_the_models_own_pick()
    {
        var model = new RecordedModel(LanguageModelResult.Success($$"""
            {"ambulance_id": "{{Fuller.Id}}",
             "rationale": "WP-CAL-102 is 8 minutes away, 2 minutes behind the fastest, and has a full crew of 3 for a critical call."}
            """));

        var advice = await Advisor(model).ChooseAsync(Context);

        Assert.Equal(Fuller.Id, advice.AmbulanceId);
        Assert.Equal(DispatchRecommendationSource.Model, advice.Source);
        Assert.Null(advice.SourceNote);
        Assert.Contains("Chest pain", model.LastData);
        Assert.True(DispatchAdviceValidator.ValidateChoice(advice, Context).Passed);
    }

    [Fact]
    public async Task No_key_falls_back_to_the_fastest_and_says_why()
    {
        var advice = await Advisor(new RecordedModel(null)).ChooseAsync(Context);

        Assert.Equal(Fast.Id, advice.AmbulanceId);
        Assert.Equal(DispatchRecommendationSource.ModelUnavailable, advice.Source);
        Assert.Equal(LanguageModelFailure.NotConfigured.ToReviewerText(), advice.SourceNote);
    }

    [Fact]
    public async Task An_unreadable_answer_falls_back_to_the_fastest()
    {
        var model = new RecordedModel(LanguageModelResult.Success("""{"pick": "the red one"}"""));

        var advice = await Advisor(model).ChooseAsync(Context);

        Assert.Equal(Fast.Id, advice.AmbulanceId);
        Assert.Equal(LanguageModelFailure.BadResponse.ToReviewerText(), advice.SourceNote);
    }

    [Fact]
    public async Task A_spent_quota_is_reported_as_such()
    {
        var model = new RecordedModel(
            LanguageModelResult.Failure("429", LanguageModelFailure.QuotaExhausted));

        var advice = await Advisor(model).ChooseAsync(Context);

        Assert.Equal(DispatchRecommendationSource.ModelUnavailable, advice.Source);
        Assert.Equal(LanguageModelFailure.QuotaExhausted.ToReviewerText(), advice.SourceNote);
    }

    [Fact]
    public void A_pick_off_the_shortlist_is_rejected()
    {
        var verdict = DispatchAdviceValidator.ValidateChoice(
            new DispatchAdvice(Guid.NewGuid(), "WP-CAL-999 is closest."), Context);

        Assert.Equal(DispatchAdviceValidator.OnShortlist, verdict.FailedRule);
    }

    [Fact]
    public void A_pick_more_than_three_minutes_slower_is_rejected()
    {
        var verdict = DispatchAdviceValidator.ValidateChoice(
            new DispatchAdvice(Slow.Id, "WP-CAL-103 has a full crew."), Context);

        Assert.Equal(DispatchAdviceValidator.WithinTimeMargin, verdict.FailedRule);
    }

    [Fact]
    public void A_reason_that_does_not_name_the_pick_is_rejected()
    {
        var verdict = DispatchAdviceValidator.ValidateChoice(
            new DispatchAdvice(Fuller.Id, "This one has a full crew."), Context);

        Assert.Equal(DispatchAdviceValidator.ReasonNamesChoice, verdict.FailedRule);
    }

    [Fact]
    public void Without_road_estimates_only_the_first_ambulance_may_be_picked()
    {
        var first = Fast with { RouteMinutes = null };
        var second = Fuller with { RouteMinutes = null };
        var context = Context with { Shortlist = [first, second] };

        var verdict = DispatchAdviceValidator.ValidateChoice(
            new DispatchAdvice(second.Id, "WP-CAL-102 has a full crew."), context);

        Assert.Equal(DispatchAdviceValidator.WithinTimeMargin, verdict.FailedRule);
    }

    private static GeminiDispatchAdvisor Advisor(ILanguageModel model)
        => new(model, NullLogger<GeminiDispatchAdvisor>.Instance);

    private sealed class RecordedModel(LanguageModelResult? answer) : ILanguageModel
    {
        public bool IsConfigured => answer is not null;

        public string LastData { get; private set; } = string.Empty;

        public Task<LanguageModelResult> CompleteJsonAsync(
            string instruction, string dataJson, CancellationToken cancellationToken = default)
        {
            LastData = dataJson;
            return Task.FromResult(answer!);
        }
    }
}
