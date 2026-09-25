namespace CareLanka.Api.Agents;

/// <summary>
/// Registered when no API key is configured. It is not a stub standing in for someone's unbuilt
/// work - it is the honest answer that there is no model here, which every agent in this project
/// already has to handle for a dead key or an exhausted quota on the day.
/// </summary>
public sealed class NoLanguageModel : ILanguageModel
{
    public const string Reason =
        "No language model is configured. Set LanguageModel:ApiKey to enable ranking and "
        + "rationales; the agent falls back to its deterministic ranking without one.";

    public bool IsConfigured => false;

    public Task<LanguageModelResult> CompleteJsonAsync(
        string instruction, string dataJson, CancellationToken cancellationToken = default)
        => Task.FromResult(
            LanguageModelResult.Failure(Reason, LanguageModelFailure.NotConfigured));
}
