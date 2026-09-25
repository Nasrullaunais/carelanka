using CareLanka.Api.Agents;
using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class CareAdvisorTests
{
    [Fact]
    public async Task A_well_formed_answer_is_taken_as_the_draft()
    {
        var candidate = await Advise("""{"urgency_flag":"medium","message":"Reports a worsening headache. Suggest a bedside review."}""");

        Assert.Equal(CareUrgency.Medium, candidate.UrgencyFlag);
        Assert.Equal("Reports a worsening headache. Suggest a bedside review.", candidate.Message);
    }

    [Fact]
    public async Task An_answer_that_is_not_json_falls_back_to_the_deterministic_draft()
    {
        var candidate = await Advise("I would say this patient needs watching.");

        Assert.False(string.IsNullOrWhiteSpace(candidate.Message));
    }

    [Fact]
    public async Task An_unrecognised_urgency_value_falls_back_to_the_deterministic_draft()
    {
        var fallback = await Advise("""{"urgency_flag":"urgent","message":"Something."}""");
        var deterministic = await new DeterministicCareAdvisor().AdviseAsync(Context());

        Assert.Equal(deterministic.UrgencyFlag, fallback.UrgencyFlag);
    }

    [Fact]
    public async Task A_model_that_could_not_be_reached_falls_back_to_the_deterministic_draft()
    {
        var candidate = await Advise(LanguageModelResult.Failure(
            "the provider answered 503", LanguageModelFailure.ProviderOverloaded));

        Assert.False(string.IsNullOrWhiteSpace(candidate.Message));

        // The reviewer has to be able to tell this from a note written about their patient.
        Assert.Equal(CareDraftSource.ModelUnavailable, candidate.Source);
        Assert.Contains("busy", candidate.SourceNote);
    }

    [Fact]
    public async Task With_no_key_configured_the_model_is_not_called_at_all()
    {
        var model = new FakeLanguageModel(LanguageModelResult.Success("{}"), configured: false);

        await new GeminiCareAdvisor(model, NullLogger<GeminiCareAdvisor>.Instance).AdviseAsync(Context());

        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task The_deterministic_fallback_always_passes_CR1_and_CR5()
    {
        var context = new CareAdviceContext(
            "I took 500mg paracetamol for my penicillin allergy reaction",
            RedFlagMatched: false,
            new CareMedicalProfileFacts("Diabetes", "Penicillin", "Fever", null),
            null,
            new CarePatientHistoryFacts(40, Gender.Male, [], []));

        var draft = await new DeterministicCareAdvisor().AdviseAsync(context);
        var result = CareRecommendationValidator.Validate(draft, redFlagMatched: false, allergiesText: "Penicillin");

        Assert.True(result.Passed);
    }

    private static Task<CareDraftCandidate> Advise(string json)
        => Advise(LanguageModelResult.Success(json));

    private static Task<CareDraftCandidate> Advise(LanguageModelResult result)
        => new GeminiCareAdvisor(
                new FakeLanguageModel(result, configured: true),
                NullLogger<GeminiCareAdvisor>.Instance)
            .AdviseAsync(Context());

    private static CareAdviceContext Context()
        => new(
            "My headache is worse today and it hurts more when I lie flat.",
            RedFlagMatched: false,
            new CareMedicalProfileFacts("Type 2 diabetes", "Penicillin", "Headache since admission", null),
            new CareCurrentAdmissionFacts(AdmissionCategory.General, AdmissionUrgency.Routine, false, "General B", DateTimeOffset.UtcNow),
            new CarePatientHistoryFacts(34, Gender.Female, [], []));

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
