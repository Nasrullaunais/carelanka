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
        var result = Validate("A headache like that is worth a nurse looking at today.");

        Assert.True(result.Passed);
        Assert.Empty(result.FailedRules);
        Assert.Equal(CareUrgency.Medium, result.UrgencyFlag);
    }

    [Theory]
    [InlineData("Take 500mg of the tablets you were given.")]
    [InlineData("You may need 2 tablets before the ward round.")]
    public void A_dosage_fails_CR1(string message)
    {
        var result = Validate(message);

        Assert.Contains("CR1", result.FailedRules);
    }

    [Fact]
    public void A_medicine_the_patient_never_raised_fails_CR1()
    {
        var result = Validate(
            "Ibuprofen is not the right thing for this.",
            reportedText: "My headache is worse today.");

        Assert.Contains("CR1", result.FailedRules);
    }

    /// <summary>
    /// The case this rule exists for. A patient asking about the exact substance their record
    /// lists as an allergy has to be told, by name, not to take it - the old blanket ban made
    /// that the one sentence the agent could not write.
    /// </summary>
    [Fact]
    public void Naming_a_recorded_allergy_to_warn_against_it_passes()
    {
        var result = Validate(
            "Please do not take penicillin. Your record lists it as an allergy.",
            reportedText: "my headache is worse today. should i take some penicilin");

        Assert.True(result.Passed);
    }

    [Fact]
    public void Saying_what_a_medicine_the_patient_raised_is_actually_for_passes()
    {
        var result = Validate(
            "Penicillin is not a painkiller, so it would not help a headache, and it is not safe for you.",
            reportedText: "should i take some penicilin for my head");

        Assert.True(result.Passed);
    }

    [Fact]
    public void Recommending_a_recorded_allergen_fails_CR5()
    {
        var result = Validate(
            "You could ask the nurse for penicillin.",
            reportedText: "should i take some penicilin");

        Assert.Contains("CR5", result.FailedRules);
    }

    /// <summary>
    /// One warning cannot license a recommendation further down, which is why the rule is applied
    /// sentence by sentence rather than to the message as a whole.
    /// </summary>
    [Fact]
    public void A_warning_followed_by_a_recommendation_still_fails_CR5()
    {
        var result = Validate(
            "Do not take penicillin on your own. Ask the nurse for penicillin instead of waiting.",
            reportedText: "should i take some penicilin");

        Assert.Contains("CR5", result.FailedRules);
    }

    [Fact]
    public void The_patients_own_spelling_does_not_decide_whether_the_rule_fires()
    {
        // "penicilin" in the report, "Penicillin" on the record, "Penicillin" in the draft.
        var result = Validate(
            "Please do not take penicillin - your record lists it as an allergy.",
            reportedText: "should i take some penicilin");

        Assert.True(result.Passed);
    }

    [Fact]
    public void A_message_that_does_not_mention_any_recorded_allergen_passes_CR5()
    {
        var result = Validate("A nurse will come and check on you this shift.");

        Assert.True(result.Passed);
    }

    [Fact]
    public void A_red_flag_match_forces_high_urgency_even_if_the_model_said_lower()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, "Someone will come to you now."),
            redFlagMatched: true,
            allergiesText: null);

        Assert.Equal(CareUrgency.High, result.UrgencyFlag);
    }

    [Fact]
    public void With_no_recorded_allergies_CR5_never_fails()
    {
        var result = CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Low, "Tell your nurse if this gets worse."),
            redFlagMatched: false,
            allergiesText: null);

        Assert.True(result.Passed);
    }

    /// <summary>
    /// The reason CR5 was narrowed. Saying what a medicine is for sends the patient nowhere, and
    /// failing it meant every question about a medicine came back as the fallback note - the model's
    /// real answer never reached the reviewer at all.
    /// </summary>
    [Theory]
    [InlineData("Please do not take penicillin on your own. Penicillin is normally used for infections.")]
    [InlineData("Penicillin is normally used for infections. Your nurse will check your record first.")]
    public void Describing_what_a_medicine_is_for_passes_CR5(string message)
    {
        var result = Validate(message, reportedText: "can I take penicillin");

        Assert.True(result.Passed, string.Join(", ", result.FailedRules));
    }

    /// <summary>
    /// The other half of the same change: narrowing CR5 must not let a draft point the patient at
    /// a medicine. These are the sentences the rule exists for.
    /// </summary>
    [Theory]
    [InlineData("You can take penicillin if the pain gets worse.")]
    [InlineData("Try penicillin and see if it settles.")]
    [InlineData("You should ask the nurse for penicillin.")]
    public void Sending_the_patient_towards_a_medicine_still_fails_CR5(string message)
    {
        var result = Validate(message, reportedText: "can I take penicillin");

        Assert.Contains("CR5", result.FailedRules);
    }

    /// <summary>
    /// Normalise collapses doubled letters, so a draft saying "allergy" arrives as "alergy". The
    /// marker list has to be normalised too or the word never matches its own entry.
    /// </summary>
    [Fact]
    public void An_allergy_word_counts_as_saying_not_to()
    {
        var result = Validate(
            "You are allergic to penicillin, so ask your nurse before anything is given to you.",
            reportedText: "can I take penicillin");

        Assert.True(result.Passed, string.Join(", ", result.FailedRules));
    }

    private static CareValidationResult Validate(
        string message, string? reportedText = null)
        => CareRecommendationValidator.Validate(
            new CareDraftCandidate(CareUrgency.Medium, message),
            redFlagMatched: false,
            allergiesText: "Penicillin, Latex",
            reportedText: reportedText);
}
