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

    /// <summary>
    /// The free tier meters requests per day per model, so the model name is also the quota
    /// bucket - <c>gemini-3.6-flash</c> allows 20 a day, which a few failed runs spend.
    /// <para>
    /// <c>gemini-3.5-flash</c> is the settled choice (2026-09-21). On the same one-line prompt and
    /// the same key, 3.6 answered in 95s, 25s and 77s where 3.5 answered in 12s - free-tier queue
    /// time, not reasoning, since both reported no thinking tokens. 3.6's published gains are token
    /// economy and agentic tool loops, neither of which this one-call agent uses.
    /// </para>
    /// <para>
    /// Support for <see cref="ThinkingBudget"/> varies by model - <c>gemini-3.5-flash-lite</c>
    /// rejects it outright with a 400 - so re-check both together when changing this.
    /// </para>
    /// </summary>
    public string Model { get; set; } = "gemini-3.5-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>
    /// Tokens the model may spend reasoning before it starts writing, sent as
    /// <c>generationConfig.thinkingConfig.thinkingBudget</c>. Gemini 3 reasons by default, which is
    /// what made a one-paragraph reply take 12-41 seconds and time out more often than not; our
    /// prompts are short and heavily constrained, so that was buying the wait and not the answer.
    /// Null sends nothing and lets the provider choose.
    /// </summary>
    /// <remarks>
    /// The documented <c>generationConfig.thinkingLevel</c> is not a field this API version has -
    /// v1beta rejects it outright with "Unknown name thinkingLevel at generation_config: Cannot
    /// find field". <c>thinkingConfig</c> is the shape it accepts. Verified against the live API on
    /// 2026-09-21; do not "correct" it back to the one the docs show without re-checking.
    /// </remarks>
    public int? ThinkingBudget { get; set; }

    /// <summary>
    /// Hard timeout per attempt. §9.1 asks for one by name. Measured against the free tier on
    /// 2026-09-21, a call that succeeds takes 12-41 seconds, so the old 20 was hanging up on
    /// answers that were on their way. Raised to 60 on top of <see cref="ThinkingBudget"/>: three
    /// attempts that each hang up a second before the answer arrives waste the whole run, and one
    /// attempt that waits is cheaper than three that do not.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

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
    /// them; lower it and the third attempt is the one that stops happening. At 60s an attempt
    /// that is 3 x 60 plus a 2s and a 4s backoff.
    /// </summary>
    public int TotalBudgetSeconds { get; set; } = 190;
}
