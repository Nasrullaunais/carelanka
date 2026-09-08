using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_endpoint_maps_a_healthy_service_result_to_200()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadFromJsonAsync<HealthStatus>();

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("healthy", body?.Status);
        Assert.Equal("up", body?.Database);
    }

    private sealed class TestApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHealthService>();
                services.AddSingleton<IHealthService>(new StubHealthService());
            });
        }
    }

    private sealed class StubHealthService : IHealthService
    {
        public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthStatus
            {
                Status = "healthy",
                Database = "up",
                Version = "test",
                CheckedAt = DateTimeOffset.UtcNow
            });
    }
}
