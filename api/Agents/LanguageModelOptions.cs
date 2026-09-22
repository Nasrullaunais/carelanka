namespace CareLanka.Api.Agents;

public sealed class LanguageModelOptions
{
    public const string SectionName = "LanguageModel";

    /// <summary>
    /// Read from configuration, never from source. Empty in the repository and in every committed
    /// appsettings file; supplied as an environment variable. With no key the API still starts and
    /// every agent still answers - see <see cref="NoLanguageModel"/>.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.6-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// Hard timeout per attempt. §9.1 asks for one by name. Measured against the free tier on
    /// 2026-09-21, a call that succeeds takes 12-41 seconds, so the old 20 was hanging up on
    /// answers that were on their way.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Retries after the first attempt, so 2 means at most three calls. Three is the deliberate
    /// ceiling: a provider still refusing on the third try is having a bad minute, and a fourth
    /// call spends quota to find that out again.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// First backoff, doubling each retry and capped at <see cref="MaxBackoffSeconds"/>. Set to 0
    /// to retry immediately, which is what the tests do.
    /// </summary>
    public int RetryBackoffSeconds { get; set; } = 2;

    public int MaxBackoffSeconds { get; set; } = 15;

    /// <summary>
    /// How long the whole call may take, retries and waiting included. Nobody is sitting in front
    /// of this - a draft goes into a queue a nurse reads when they have a moment - so it is worth
    /// waiting out a busy provider rather than falling back to the standard note in five seconds.
    /// Wide enough for three attempts at <see cref="TimeoutSeconds"/> plus the backoff between
    /// them; lower it and the third attempt is the one that stops happening.
    /// </summary>
    public int TotalBudgetSeconds { get; set; } = 150;
}
