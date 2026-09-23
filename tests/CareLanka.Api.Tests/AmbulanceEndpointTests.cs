using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AmbulanceEndpointTests
{
    private readonly ApiApplication _application;

    public AmbulanceEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Duty_manager_can_register_an_available_ambulance()
    {
        using var client = await ManagerClientAsync();
        var registrationNumber = $"WP-CAB-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var response = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = registrationNumber,
            current_latitude = 6.927079,
            current_longitude = 79.861244
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ambulance = body.RootElement;

        Assert.Equal(registrationNumber, ambulance.GetProperty("registration_number").GetString());
        Assert.Equal("available", ambulance.GetProperty("status").GetString());
        Assert.True(ambulance.GetProperty("is_active").GetBoolean());

        var id = ambulance.GetProperty("id").GetGuid();
        var detail = await client.GetAsync($"/api/ambulances/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var list = await client.GetAsync($"/api/ambulances?search={registrationNumber}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = listBody.RootElement.GetProperty("items");
        Assert.Single(items.EnumerateArray());
        Assert.Equal(id, items[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Duty_manager_can_update_retire_and_reinstate_an_ambulance()
    {
        using var client = await ManagerClientAsync();
        var registrationNumber = $"WP-RET-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var created = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = registrationNumber
        });
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdBody.RootElement.GetProperty("id").GetGuid();

        var updated = await client.PatchAsJsonAsync($"/api/ambulances/{id}", new
        {
            status = "out_of_service",
            out_of_service_reason = "Scheduled service"
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal("out_of_service", updatedBody.RootElement.GetProperty("status").GetString());

        var retired = await client.PostAsJsonAsync($"/api/ambulances/{id}/retire", new
        {
            reason = "Vehicle replaced"
        });
        Assert.Equal(HttpStatusCode.NoContent, retired.StatusCode);

        var defaultList = await client.GetAsync($"/api/ambulances?search={registrationNumber}");
        using var defaultBody = JsonDocument.Parse(await defaultList.Content.ReadAsStringAsync());
        Assert.Empty(defaultBody.RootElement.GetProperty("items").EnumerateArray());

        var retiredList = await client.GetAsync(
            $"/api/ambulances?search={registrationNumber}&includeRetired=true");
        using var retiredBody = JsonDocument.Parse(await retiredList.Content.ReadAsStringAsync());
        Assert.Single(retiredBody.RootElement.GetProperty("items").EnumerateArray());

        var reinstated = await client.PostAsync($"/api/ambulances/{id}/reinstate", null);
        Assert.Equal(HttpStatusCode.OK, reinstated.StatusCode);

        using var reinstatedBody = JsonDocument.Parse(await reinstated.Content.ReadAsStringAsync());
        Assert.True(reinstatedBody.RootElement.GetProperty("is_active").GetBoolean());
        Assert.Equal("available", reinstatedBody.RootElement.GetProperty("status").GetString());

        await using var scope = _application.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var history = await db.AmbulanceStatusHistory.Where(item => item.AmbulanceId == id)
            .OrderBy(item => item.StartedAt).Select(item => item.Status).ToListAsync();
        Assert.Equal([AmbulanceStatus.Available, AmbulanceStatus.OutOfService, AmbulanceStatus.Available], history);
    }

    [Fact]
    public async Task Two_active_ambulances_cannot_share_a_registration_number()
    {
        using var client = await ManagerClientAsync();
        var registrationNumber = $"WP-DUP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var first = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = registrationNumber
        });
        var duplicate = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = registrationNumber.ToLowerInvariant()
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("cl_emg_001", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Nearest_ranking_follows_drive_time_and_puts_ambulances_without_a_position_last()
    {
        using var setup = await ManagerClientAsync();
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var shortDrive = await RegisterAsync(setup, $"WP-SD-{tag}", 6.95, 79.90);
        var longDrive = await RegisterAsync(setup, $"WP-LD-{tag}", 6.92, 79.87);
        var unlocated = await RegisterAsync(setup, $"WP-NP-{tag}", null, null);
        var fakeRoads = new Dictionary<Guid, AmbulanceTravel>
        {
            [shortDrive] = new(9.0, 300),
            [longDrive] = new(1.0, 900)
        };

        using var application = _application.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IAmbulanceDistanceService>(new FakeRoads(fakeRoads)))));
        using var client = await ManagerClientAsync(application);
        var response = await client.GetAsync($"/api/ambulances?search=-{tag}&nearToLatitude=6.9186&nearToLongitude=79.8686&sortBy=distance&sortDir=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([shortDrive, longDrive, unlocated], items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(5, items[0].GetProperty("drive_minutes").GetInt32());
        Assert.False(items[0].GetProperty("is_straight_line_distance").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[2].GetProperty("distance_km").ValueKind);
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, string registrationNumber, double? latitude, double? longitude)
    {
        var response = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = registrationNumber,
            current_latitude = latitude,
            current_longitude = longitude
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private sealed class FakeRoads(Dictionary<Guid, AmbulanceTravel> travel) : IAmbulanceDistanceService
    {
        public Task<DistanceMeasurement> MeasureAsync(
            IReadOnlyCollection<AmbulanceLocation> ambulances,
            decimal destinationLatitude,
            decimal destinationLongitude,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DistanceMeasurement(travel, IsStraightLine: false));
    }

    private Task<HttpClient> ManagerClientAsync() => ManagerClientAsync(_application);

    private static async Task<HttpClient> ManagerClientAsync(WebApplicationFactory<Program> application)
    {
        var client = application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiApplication.ManagerEmail,
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        return client;
    }
}
