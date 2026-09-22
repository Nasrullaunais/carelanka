namespace CareLanka.Api.Agents;

/// <summary>
/// Why a model call did not produce an answer. The distinction that matters operationally is
/// <see cref="QuotaExhausted"/> versus <see cref="ProviderOverloaded"/>: the first is our allowance
/// being spent and retrying only spends more of it, the second is the provider being busy and
/// usually passes on its own.
/// </summary>
public enum LanguageModelFailure
{
    None,
    NotConfigured,
    QuotaExhausted,
    ProviderOverloaded,
    Timeout,
    Unreachable,
    BadResponse
}

public static class LanguageModelFailureText
{
    /// <summary>
    /// Written for the nurse or doctor reading the review screen, not for a log: it has to say
    /// whether somebody needs to do something about it.
    /// </summary>
    public static string ToReviewerText(this LanguageModelFailure failure) => failure switch
    {
        LanguageModelFailure.NotConfigured =>
            "No AI model is set up, so the standard backup note was used instead.",
        LanguageModelFailure.QuotaExhausted =>
            "The AI model's free tier limit is used up, so the standard backup note was used "
            + "instead. A new API key is needed before the agent can write real drafts again.",
        LanguageModelFailure.ProviderOverloaded =>
            "The AI model stayed busy for as long as the agent kept trying, so the standard backup "
            + "reply was used instead. This usually clears on its own - try again.",
        LanguageModelFailure.Timeout =>
            "The AI model took too long to answer, so the standard backup note was used instead. "
            + "Try again.",
        LanguageModelFailure.Unreachable =>
            "The AI model could not be reached, so the standard backup note was used instead. "
            + "Try again.",
        LanguageModelFailure.BadResponse =>
            "The AI model's answer could not be read, so the standard backup note was used "
            + "instead. Try again.",
        _ => "The standard backup note was used instead of an AI draft."
    };

    /// <summary>
    /// Retrying a spent allowance cannot succeed and spends more of it; an unconfigured key cannot
    /// fix itself either. Everything else is worth another go.
    /// </summary>
    public static bool IsWorthRetrying(this LanguageModelFailure failure)
        => failure is not (LanguageModelFailure.QuotaExhausted or LanguageModelFailure.NotConfigured);
}
