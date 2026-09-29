using System.Net;
using System.Net.Http.Json;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class ProblemResponseTests
{
    [Fact]
    public async Task Protected_endpoint_without_token_returns_problem_json()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Exception_handler_returns_problem_json()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new ThrowingTestApplication(new ConflictException());
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_refused_request_is_logged_once_as_information_not_as_an_error()
    {
        using var environment = TestEnvironment.Use();
        var logs = new CapturedLogs();
        await using var application = new ThrowingTestApplication(new ConflictException(), logs);
        using var client = application.CreateClient();

        await client.GetAsync("/api/health");

        Assert.Empty(logs.AtOrAbove(LogLevel.Warning));
        Assert.Single(logs.AtOrAbove(LogLevel.Information), entry => entry.Exception is null && entry.Message.Contains("/api/health"));
    }

    [Fact]
    public async Task An_unexpected_failure_is_logged_once_as_an_error_with_its_exception()
    {
        using var environment = TestEnvironment.Use();
        var logs = new CapturedLogs();
        var failure = new InvalidOperationException("boom");
        await using var application = new ThrowingTestApplication(failure, logs);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var error = Assert.Single(logs.AtOrAbove(LogLevel.Error));
        Assert.Same(failure, error.Exception);
    }

    [Fact]
    public async Task Rate_limit_rejection_returns_problem_json()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= 20; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        }

        using var finalResponse = response!;
        Assert.Equal(HttpStatusCode.TooManyRequests, finalResponse.StatusCode);
        Assert.Equal("application/problem+json", finalResponse.Content.Headers.ContentType?.MediaType);
    }

    private sealed class TestApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseEnvironment("Testing");
    }

    private sealed class ThrowingTestApplication(Exception exception, CapturedLogs? logs = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHealthService>();
                services.AddSingleton<IHealthService>(new ThrowingHealthService(exception));
            });

            if (logs is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(logs));
            }
        }
    }

    private sealed class ThrowingHealthService(Exception exception) : IHealthService
    {
        public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
            => throw exception;
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> AtOrAbove(LogLevel level)
        {
            lock (_entries)
            {
                return _entries.Where(entry => entry.Level >= level && IsErrorHandling(entry.Category)).ToList();
            }
        }

        // Only the two places that log a failed request; background workers log their own things.
        private static bool IsErrorHandling(string category)
            => category == typeof(ApiExceptionHandler).FullName
               || category.StartsWith("Microsoft.AspNetCore.Diagnostics", StringComparison.Ordinal);

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Logger(CapturedLogs owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (owner._entries)
                {
                    owner._entries.Add(new LogEntry(category, logLevel, formatter(state, exception), exception));
                }
            }
        }
    }

}
