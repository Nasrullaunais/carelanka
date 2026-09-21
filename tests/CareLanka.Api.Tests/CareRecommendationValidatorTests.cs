using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Data.Enums;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// Rules CR1-CR5 are the safety net between whatever the model wrote and a reviewer's screen, so
/// they are tested directly against the deterministic validator rather than through the model.
/// </summary>
public sealed class CareRecommendationValidatorTests
{
    [Fact]
    public void A_clean_draft_passes()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Medium, "Reports a worsening headache. Suggest a bedside review this shift."),
            redFlagMatched: false,
            allergiesText: "Penicillin");

        Assert.True(result.Passed);
        Assert.Empty(result.FailedRules);
        Assert.Equal(CareUrgency.Medium, result.UrgencyFlag);
    }

    [Theory]
    [InlineData("Give 500mg paracetamol now.")]
    [InlineData("Consider ibuprofen for the pain.")]
    [InlineData("Patient may need 2 tablets before the ward round.")]
    public void A_drug_name_or_dosage_fails_CR1(string message)
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, message), redFlagMatched: false, allergiesText: null);

        Assert.False(result.Passed);
        Assert.Contains("CR1", result.FailedRules);
    }

    [Fact]
    public void Naming_a_recorded_allergy_fails_CR5()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, "No concerns other than a known penicillin sensitivity to watch."),
            redFlagMatched: false,
            allergiesText: "Penicillin, Latex");

        Assert.False(result.Passed);
        Assert.Contains("CR5", result.FailedRules);
    }

    [Fact]
    public void A_message_that_does_not_mention_any_recorded_allergen_passes_CR5()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, "Suggest a routine check this shift."),
            redFlagMatched: false,
            allergiesText: "Penicillin, Latex");

        Assert.True(result.Passed);
    }

    [Fact]
    public void A_red_flag_match_forces_high_urgency_even_if_the_model_said_lower()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, "Routine review suggested."),
            redFlagMatched: true,
            allergiesText: null);

        Assert.Equal(CareUrgency.High, result.UrgencyFlag);
    }

    [Fact]
    public void With_no_recorded_allergies_CR5_never_fails()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, "Reports feeling generally unwell since this morning."),
            redFlagMatched: false,
            allergiesText: null);

        Assert.True(result.Passed);
    }
}
