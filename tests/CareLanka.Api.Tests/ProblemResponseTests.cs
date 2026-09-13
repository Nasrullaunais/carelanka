using System.Net;
using System.Net.Http.Json;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        await using var application = new ThrowingTestApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Specific_api_exception_is_handled_before_its_base_exception()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new ThrowingTestApplication(
            new IllegalTransitionException("Admission", "awaiting_bed", "admitted"));
        using var client = application.CreateClient();

        using var httpResponse = await client.GetAsync("/api/health");
        var response = await httpResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(response);
        Assert.Equal(StatusCodes.Status409Conflict, response.Status);
        Assert.Equal(MessageCode.IllegalTransition.ToWire(), response.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Unexpected_exception_uses_the_generic_internal_server_error_response()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new ThrowingTestApplication(new InvalidOperationException("secret"));
        using var client = application.CreateClient();

        using var httpResponse = await client.GetAsync("/api/health");
        var response = await httpResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(response);
        Assert.Equal(StatusCodes.Status500InternalServerError, response.Status);
        Assert.Equal(MessageCode.Unexpected.ToWire(), response.Extensions["code"]?.ToString());
        Assert.DoesNotContain("secret", response.Detail);
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

    private sealed class ThrowingTestApplication : WebApplicationFactory<Program>
    {
        private readonly Exception _exception;

        public ThrowingTestApplication()
            : this(new ConflictException())
        {
        }

        public ThrowingTestApplication(Exception exception) => _exception = exception;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHealthService>();
                services.AddSingleton<IHealthService>(new ThrowingHealthService(_exception));
            });
        }
    }

    private sealed class ThrowingHealthService : IHealthService
    {
        private readonly Exception _exception;

        public ThrowingHealthService(Exception exception) => _exception = exception;

        public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
            => throw _exception;
    }

}
