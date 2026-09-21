namespace CareLanka.Api.Agents;

/// <summary>
/// The one thing an agent knows about a language model: hand it a fixed instruction and a JSON
/// payload of data, get JSON back. Which provider answered is a DI registration and nothing in an
/// agent can tell (ADR 2).
/// </summary>
/// <remarks>
/// It never throws. A dead key, an exhausted quota or a timeout is an ordinary
/// <see cref="LanguageModelResult"/> with <c>Ok = false</c>, because every caller in this project
/// has a deterministic answer to fall back to and "the model was unavailable" is a step result
/// worth persisting rather than an exception worth unwinding.
/// </remarks>
public interface ILanguageModel
{
    /// <summary>
    /// False when no provider is configured. Callers use it to skip the call and record why,
    /// rather than to change what they produce.
    /// </summary>
    bool IsConfigured { get; }

    Task<LanguageModelResult> CompleteJsonAsync(
        string instruction, string dataJson, CancellationToken cancellationToken = default);
}

public sealed record LanguageModelResult(
    bool Ok, string? Json, string? Error, LanguageModelFailure Reason = LanguageModelFailure.None)
{
    public static LanguageModelResult Success(string json) => new(true, json, null);

    public static LanguageModelResult Failure(string error, LanguageModelFailure reason)
        => new(false, null, error, reason);
}
