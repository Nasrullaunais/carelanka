using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FluentValidationIntegrationTests
{
    private readonly ApiApplication _application;

    public FluentValidationIntegrationTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Invalid_input_returns_the_published_problem_without_entering_business_logic()
    {
        var tracker = new TrackingAmbulanceService();
        using var application = _application.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAmbulanceService>();
                services.AddSingleton<IAmbulanceService>(tracker);
            }));
        using var client = application.CreateClient();
        await AuthorizeAsManagerAsync(client);

        var response = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = "WP-FV-TEST",
            current_latitude = 6.927079
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.TryGetProperty("errors", out _));
        Assert.False(tracker.CreateWasCalled);
    }

    [Fact]
    public async Task Invalid_query_returns_the_published_problem_without_entering_business_logic()
    {
        var tracker = new TrackingAmbulanceService();
        using var application = _application.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAmbulanceService>();
                services.AddSingleton<IAmbulanceService>(tracker);
            }));
        using var client = application.CreateClient();
        await AuthorizeAsManagerAsync(client);

        var response = await client.GetAsync("/api/ambulances?nearToLatitude=6.9");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(tracker.ListWasCalled);
    }

    private static async Task AuthorizeAsManagerAsync(HttpClient client)
    {
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiApplication.ManagerEmail,
            password = ApiApplication.Password
        });
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
    }

    private sealed class TrackingAmbulanceService : IAmbulanceService
    {
        public bool CreateWasCalled { get; private set; }

        public bool ListWasCalled { get; private set; }

        public Task<PagedResult<AmbulanceSummary>> ListAsync(
            AmbulanceListRequest request,
            CancellationToken cancellationToken = default)
        {
            ListWasCalled = true;
            throw new NotSupportedException();
        }

        public Task<AmbulanceDetail> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Ambulance> CreateAsync(
            CreateAmbulanceRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateWasCalled = true;
            return Task.FromResult(new Ambulance());
        }

        public Task<Ambulance> UpdateAsync(
            Guid id,
            UpdateAmbulanceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RetireAsync(
            Guid id,
            RetireAmbulanceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Ambulance> ReinstateAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReportLocationAsync(Guid id, ReportAmbulanceLocationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
