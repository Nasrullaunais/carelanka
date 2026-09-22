using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Agents;

/// <summary>
/// Google Gemini free tier (ADR 2), reached over its REST API in JSON mode so the structured
/// output assignment §9.1 asks for is a well-formed object rather than one repaired by hand.
/// </summary>
/// <remarks>
/// The API key goes in the <c>x-goog-api-key</c> header rather than the query string, so it never
/// reaches a request log.
/// </remarks>
public sealed class GeminiLanguageModel : ILanguageModel
{
    public const string HttpClientName = "gemini";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _clients;
    private readonly LanguageModelOptions _options;
    private readonly ILogger<GeminiLanguageModel> _log;

    public GeminiLanguageModel(
        IHttpClientFactory clients,
        IOptions<LanguageModelOptions> options,
        ILogger<GeminiLanguageModel> log)
    {
        _clients = clients;
        _options = options.Value;
        _log = log;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<LanguageModelResult> CompleteJsonAsync(
        string instruction, string dataJson, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return LanguageModelResult.Failure(
                NoLanguageModel.Reason, LanguageModelFailure.NotConfigured);
        }

        var generationConfig = new Dictionary<string, object>
        {
            ["responseMimeType"] = "application/json",
            ["temperature"] = 0.2
        };

        if (_options.ThinkingBudget is { } thinkingBudget)
        {
            generationConfig["thinkingConfig"] = new Dictionary<string, object>
            {
                ["thinkingBudget"] = thinkingBudget
            };
        }

        var body = JsonSerializer.Serialize(
            new
            {
                systemInstruction = new { parts = new[] { new { text = instruction } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = dataJson } } }
                },
                generationConfig
            },
            Json);

        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        var budget = TimeSpan.FromSeconds(Math.Max(1, _options.TotalBudgetSeconds));
        var elapsed = Stopwatch.StartNew();

        string lastError = "The language model did not answer.";
        var lastFailure = LanguageModelFailure.Unreachable;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var text = await CallAsync(body, ct);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    _log.LogInformation(
                        "Gemini answered in {Seconds:n1}s on attempt {Attempt}.",
                        elapsed.Elapsed.TotalSeconds, attempt);

                    return LanguageModelResult.Success(text);
                }

                lastError = "The language model returned an empty response.";
                lastFailure = LanguageModelFailure.BadResponse;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastError = $"The language model timed out after {_options.TimeoutSeconds}s.";
                lastFailure = LanguageModelFailure.Timeout;
            }
            catch (HttpRequestException exception)
            {
                lastError = $"The language model could not be reached: {exception.Message}";
                lastFailure = Classify(exception.StatusCode);
            }
            catch (JsonException exception)
            {
                lastError = $"The language model returned a malformed response: {exception.Message}";
                lastFailure = LanguageModelFailure.BadResponse;
            }

            _log.LogWarning(
                "Gemini attempt {Attempt} of {Attempts} failed: {Error}",
                attempt, attempts, lastError);

            if (!lastFailure.IsWorthRetrying())
            {
                _log.LogWarning(
                    "Not retrying: {Reason} cannot succeed on a retry.", lastFailure);

                break;
            }

            if (attempt >= attempts)
            {
                break;
            }

            // The provider being busy is the common case on the free tier. Back off and keep
            // waiting: nobody is watching this run, so giving up in five seconds buys nothing.
            var wait = Backoff(attempt);

            if (elapsed.Elapsed + wait >= budget)
            {
                _log.LogWarning(
                    "Giving up on Gemini after {Elapsed:n0}s of a {Budget:n0}s budget.",
                    elapsed.Elapsed.TotalSeconds, budget.TotalSeconds);

                break;
            }

            await Task.Delay(wait, ct);
        }

        return LanguageModelResult.Failure(lastError, lastFailure);
    }

    /// <summary>
    /// Doubles each retry - 2s, 4s, 8s - capped, so six attempts spread over a minute or so rather
    /// than hammering a provider that has just said it is busy.
    /// </summary>
    private TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Max(0, _options.RetryBackoffSeconds) * Math.Pow(2, attempt - 1);

        return TimeSpan.FromSeconds(Math.Min(seconds, Math.Max(0, _options.MaxBackoffSeconds)));
    }

    private static LanguageModelFailure Classify(HttpStatusCode? status) => status switch
    {
        HttpStatusCode.TooManyRequests => LanguageModelFailure.QuotaExhausted,
        HttpStatusCode.PaymentRequired => LanguageModelFailure.QuotaExhausted,
        HttpStatusCode.ServiceUnavailable => LanguageModelFailure.ProviderOverloaded,
        HttpStatusCode.InternalServerError => LanguageModelFailure.ProviderOverloaded,
        HttpStatusCode.BadGateway => LanguageModelFailure.ProviderOverloaded,
        HttpStatusCode.GatewayTimeout => LanguageModelFailure.Timeout,
        _ => LanguageModelFailure.Unreachable
    };

    private async Task<string?> CallAsync(string body, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var client = _clients.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.Model}:generateContent")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-goog-api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            // The provider says why in the body - a rejected field, a spent quota, a blocked key.
            // Without it every failure reads as the same opaque status code.
            var detail = await ReasonAsync(response, timeout.Token);

            throw new HttpRequestException(
                $"the provider answered {(int)response.StatusCode}{detail}", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

        return ExtractText(document.RootElement);
    }

    /// <summary>
    /// Read for a log line, so it must never throw: a failure reading why a call failed would
    /// replace the reason with a second, less useful one.
    /// </summary>
    private static async Task<string> ReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            using var document = JsonDocument.Parse(body);

            var message = document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var text)
                    ? text.GetString()
                    : null;

            var reason = (message ?? body).ReplaceLineEndings(" ");

            return $" - {(reason.Length <= 400 ? reason : reason[..400])}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Navigated defensively rather than with GetProperty: a safety block or a quota notice comes
    /// back as a 200 with no candidate at all, and an exception for a missing key would read as a
    /// bug in us rather than an answer we did not get.
    /// </summary>
    private static string? ExtractText(JsonElement root)
        => root.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array
            && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var text)
                ? text.GetString()
                : null;
}
