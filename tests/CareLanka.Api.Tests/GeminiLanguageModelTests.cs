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
    public async Task A_spent_daily_allowance_is_not_retried()
    {
        var (model, handler) = Build(HttpStatusCode.TooManyRequests, body: QuotaRefusal("PerDay"));

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.Equal(LanguageModelFailure.QuotaExhausted, result.Reason);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// The per-minute limit clears by itself, so it is waited out like a busy provider rather than
    /// reported to the reviewer as an allowance that needs a new key.
    /// </summary>
    [Fact]
    public async Task The_per_minute_limit_is_retried_and_not_reported_as_a_spent_allowance()
    {
        var (model, handler) = Build(HttpStatusCode.TooManyRequests, body: QuotaRefusal("PerMinute"));

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.Equal(LanguageModelFailure.ProviderOverloaded, result.Reason);
        Assert.Equal(3, handler.Calls);
    }

    private static string QuotaRefusal(string window) => """
        {"error":{"code":429,"message":"You exceeded your current quota.","status":"RESOURCE_EXHAUSTED",
        "details":[{"@type":"type.googleapis.com/google.rpc.QuotaFailure",
        "violations":[{"quotaId":"GenerateRequestsWINDOWPerProjectPerModel-FreeTier"}]}]}}
        """.Replace("WINDOW", window);

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
    /// The budget goes under <c>thinkingConfig</c>, not as a <c>thinkingLevel</c> beside
    /// <c>temperature</c>: v1beta rejects the latter outright with a 400, which reads on the review
    /// screen as "could not be reached" and costs a whole run. This is the shape the live API
    /// accepted on 2026-09-21.
    /// </summary>
    [Fact]
    public async Task The_thinking_budget_is_sent_under_thinking_config()
    {
        var (model, handler) = Build(
            HttpStatusCode.ServiceUnavailable,
            configure: options => options.ThinkingBudget = 0);

        await model.CompleteJsonAsync("instruction", "{}");

        using var sent = JsonDocument.Parse(handler.LastBody!);
        var config = sent.RootElement.GetProperty("generationConfig");

        Assert.False(config.TryGetProperty("thinkingLevel", out _));
        Assert.Equal(0, config.GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
    }

    [Fact]
    public async Task No_thinking_budget_leaves_the_choice_to_the_provider()
    {
        var (model, handler) = Build(
            HttpStatusCode.ServiceUnavailable,
            configure: options => options.ThinkingBudget = null);

        await model.CompleteJsonAsync("instruction", "{}");

        using var sent = JsonDocument.Parse(handler.LastBody!);

        Assert.False(
            sent.RootElement.GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _));
    }

    /// <summary>
    /// The provider says why in the body. Without it, a rejected field and an unreachable host are
    /// the same log line, which is what made the 400 above take a round of guessing to find.
    /// </summary>
    [Fact]
    public async Task The_providers_own_reason_reaches_the_error()
    {
        var (model, _) = Build(
            HttpStatusCode.BadRequest,
            body: """{"error":{"code":400,"message":"Unknown name \"thinkingLevel\""}}""");

        var result = await model.CompleteJsonAsync("instruction", "{}");

        Assert.False(result.Ok);
        Assert.Contains("Unknown name", result.Error);
    }

    private static (GeminiLanguageModel Model, CountingHandler Handler) Build(
        HttpStatusCode status,
        string apiKey = "test-key",
        Action<LanguageModelOptions>? configure = null,
        string body = "{}")
    {
        var handler = new CountingHandler(status, body);
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
        private readonly string _body;

        public CountingHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

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
                Content = new StringContent(_body)
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
