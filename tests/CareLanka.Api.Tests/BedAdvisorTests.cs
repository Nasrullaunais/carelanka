using CareLanka.Api.Agents;
using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// The model is the only part of a bed run that is not deterministic, so what it is allowed to
/// come back with is the thing worth testing. Everything it says that is not a bed number off the
/// shortlist is discarded, and the ranked pick stands.
/// </summary>
public sealed class BedAdvisorTests
{
    [Fact]
    public async Task A_bed_from_the_shortlist_is_taken_with_its_reason()
    {
        var advice = await Advise("""{"bed_number":"GEN-02","reason":"Nearer the nurses' station."}""");

        Assert.Equal("GEN-02", advice.BedNumber);
        Assert.Equal("Nearer the nurses' station.", advice.Reason);
    }

    [Fact]
    public async Task A_bed_that_is_not_on_the_shortlist_is_ignored()
    {
        var advice = await Advise("""{"bed_number":"ICU-01","reason":"He looks unwell."}""");

        Assert.Null(advice.BedNumber);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_leaves_the_ranked_pick_standing()
    {
        var advice = await Advise("I would put him in ICU-01.");

        Assert.Equal(BedAdvice.None, advice);
    }

    [Fact]
    public async Task A_model_that_could_not_be_reached_leaves_the_ranked_pick_standing()
    {
        var advice = await Advise(LanguageModelResult.Failure(
            "the provider answered 503", LanguageModelFailure.ProviderOverloaded));

        Assert.Equal(BedAdvice.None, advice);
    }

    [Fact]
    public async Task With_no_key_configured_the_model_is_not_called_at_all()
    {
        var model = new FakeLanguageModel(LanguageModelResult.Success("{}"), configured: false);

        await new GeminiBedAdvisor(model, NullLogger<GeminiBedAdvisor>.Instance)
            .AdviseAsync(Context());

        Assert.Equal(0, model.Calls);
    }

    private static Task<BedAdvice> Advise(string json)
        => Advise(LanguageModelResult.Success(json));

    private static Task<BedAdvice> Advise(LanguageModelResult result)
        => new GeminiBedAdvisor(
                new FakeLanguageModel(result, configured: true),
                NullLogger<GeminiBedAdvisor>.Instance)
            .AdviseAsync(Context());

    private static BedAdviceContext Context()
        => new(
            AdmissionCategory.Inpatient,
            Age: 40,
            Gender.Male,
            AdmissionUrgency.Routine,
            IsInfectious: false,
            new PatientNotes("Type 2 diabetes", null, "Chest infection", null),
            [Candidate("GEN-01"), Candidate("GEN-02")]);

    private static BedAdviceCandidate Candidate(string bedNumber)
        => new(
            bedNumber,
            "General Ward",
            WardType.General,
            HasIsolation: false,
            FreeBedsInWard: 4,
            UsableBedsInWard: 10,
            SeenThisWardBefore: false,
            ["General Ward is at the right care level for a general patient."]);

    private sealed class FakeLanguageModel : ILanguageModel
    {
        private readonly LanguageModelResult _result;

        public FakeLanguageModel(LanguageModelResult result, bool configured)
        {
            _result = result;
            IsConfigured = configured;
        }

        public bool IsConfigured { get; }

        public int Calls { get; private set; }

        public Task<LanguageModelResult> CompleteJsonAsync(
            string instruction, string dataJson, CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(_result);
        }
    }
}
