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

        var body = JsonSerializer.Serialize(
            new
            {
                systemInstruction = new { parts = new[] { new { text = instruction } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = dataJson } } }
                },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.2 }
            },
            Json);

        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        string lastError = "The language model did not answer.";
        var lastFailure = LanguageModelFailure.Unreachable;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var text = await CallAsync(body, ct);

                if (!string.IsNullOrWhiteSpace(text))
                {
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

            if (attempt < attempts)
            {
                // The provider being busy is the common case and it usually clears within a
                // second or two, so back off rather than spending all three attempts at once.
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }

        return LanguageModelResult.Failure(lastError, lastFailure);
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
            throw new HttpRequestException(
                $"the provider answered {(int)response.StatusCode}", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

        return ExtractText(document.RootElement);
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
