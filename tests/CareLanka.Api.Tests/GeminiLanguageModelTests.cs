using System.Net;
using System.Text.Json;
using CareLanka.Api.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// The distinction these lock in is the one that costs money: a spent allowance must not be
/// retried, because every retry spends more of an allowance that is already gone and cannot
/// succeed. A busy provider is the opposite - it usually clears, so it is worth another go.
/// </summary>
public sealed class GeminiLanguageModelTests
{
    [Fact]
    public async Task A_spent_allowance_is_not_retried()
    {
        var (model, handler) = Build(HttpStatusCode.TooManyRequests);

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.Equal(LanguageModelFailure.QuotaExhausted, result.Reason);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_busy_provider_is_retried_to_the_configured_budget()
    {
        var (model, handler) = Build(HttpStatusCode.ServiceUnavailable);

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.Equal(LanguageModelFailure.ProviderOverloaded, result.Reason);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task No_key_configured_is_reported_as_such_without_calling_out()
    {
        var (model, handler) = Build(HttpStatusCode.OK, apiKey: "");

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.Equal(LanguageModelFailure.NotConfigured, result.Reason);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>
    /// A busy provider is worth waiting out, but the wait has a ceiling - otherwise a run could
    /// sit on a thread indefinitely. The budget is what stops it.
    /// </summary>
    [Fact]
    public async Task Retrying_stops_at_the_total_budget_even_with_attempts_left()
    {
        var (model, handler) = Build(
            HttpStatusCode.ServiceUnavailable,
            configure: options =>
            {
                options.MaxRetries = 20;
                options.RetryBackoffSeconds = 1;
                options.MaxBackoffSeconds = 1;
                options.TotalBudgetSeconds = 3;
            });

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.InRange(handler.Calls, 2, 4);
    }

    /// <summary>
    /// Gemini 3 reasons at "medium" unless told otherwise, which is what pushed a short reply past
    /// the per-attempt timeout and left every run on its deterministic fallback.
    /// </summary>
    [Fact]
    public async Task The_configured_thinking_level_is_sent()
    {
        var (model, handler) = Build(HttpStatusCode.ServiceUnavailable);

        await model.CompleteJsonAsync("instruction", "{}");

        using var sent = JsonDocument.Parse(handler.LastBody!);

        Assert.Equal(
            "low",
            sent.RootElement.GetProperty("generationConfig").GetProperty("thinkingLevel").GetString());
    }

    [Fact]
    public async Task An_empty_thinking_level_leaves_the_choice_to_the_provider()
    {
        var (model, handler) = Build(
            HttpStatusCode.ServiceUnavailable,
            configure: options => options.ThinkingLevel = "");

        await model.CompleteJsonAsync("instruction", "{}");

        using var sent = JsonDocument.Parse(handler.LastBody!);

        Assert.False(
            sent.RootElement.GetProperty("generationConfig").TryGetProperty("thinkingLevel", out _));
    }

    private static (GeminiLanguageModel Model, CountingHandler Handler) Build(
        HttpStatusCode status,
        string apiKey = "test-key",
        Action<LanguageModelOptions>? configure = null)
    {
        var handler = new CountingHandler(status);
        var settings = new LanguageModelOptions
        {
            ApiKey = apiKey,
            MaxRetries = 2,
            TimeoutSeconds = 5,
            RetryBackoffSeconds = 0
        };

        configure?.Invoke(settings);

        var options = Options.Create(settings);

        var model = new GeminiLanguageModel(
            new SingleClientFactory(handler), options, NullLogger<GeminiLanguageModel>.Instance);

        return (model, handler);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public CountingHandler(HttpStatusCode status) => _status = status;

        public int Calls { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent("{}")
            };
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
