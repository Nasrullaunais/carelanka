using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class StaffLookupValidationTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    [Fact]
    public async Task Staff_lookup_requires_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = new[] { Guid.NewGuid() }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_lookup_rejects_patient_token_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = new[] { Guid.NewGuid() }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_lookup_rejects_missing_staff_ids()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Staff_lookup_rejects_more_than_100_ids_with_bad_request()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("doctor", "staff"));

        var tooManyIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = tooManyIds
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Staff_lookup_empty_list_returns_200_empty_array()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new TestApplication();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<StaffLookupResult>>(content, JsonOptions);
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Staff_lookup_delegates_to_service_and_preserves_order()
    {
        using var environment = TestEnvironment.Use();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        var expectedResults = new List<StaffLookupResult>
        {
            new() { StaffId = id1, Found = true, FullName = "Dr. Silva", Role = StaffRole.Doctor, IsActive = true },
            new() { StaffId = id2, Found = true, FullName = "Nurse Perera", Role = StaffRole.WardNurse, IsActive = false },
            new() { StaffId = id3, Found = false, FullName = null, Role = null, IsActive = null }
        };

        await using var application = new TestApplication(new StubStaffLookupService(expectedResults));
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = new[] { id1, id2, id3 }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<StaffLookupResult>>(content, JsonOptions);
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        Assert.Equal(id1, results[0].StaffId);
        Assert.True(results[0].Found);
        Assert.Equal("Dr. Silva", results[0].FullName);
        Assert.Equal(StaffRole.Doctor, results[0].Role);
        Assert.True(results[0].IsActive);

        Assert.Equal(id2, results[1].StaffId);
        Assert.True(results[1].Found);
        Assert.Equal("Nurse Perera", results[1].FullName);
        Assert.Equal(StaffRole.WardNurse, results[1].Role);
        Assert.False(results[1].IsActive);

        Assert.Equal(id3, results[2].StaffId);
        Assert.False(results[2].Found);
        Assert.Null(results[2].FullName);
        Assert.Null(results[2].Role);
        Assert.Null(results[2].IsActive);
    }

    private static string CreateToken(string role, string principalType)
    {
        var claims = new[]
        {
            new Claim(CareLankaClaims.Subject, Guid.NewGuid().ToString()),
            new Claim(CareLankaClaims.Role, role),
            new Claim(CareLankaClaims.PrincipalType, principalType)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken(
            issuer: "carelanka-api",
            audience: "carelanka-clients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestApplication : WebApplicationFactory<Program>
    {
        private readonly IStaffLookupService? _stub;

        public TestApplication(IStaffLookupService? stub = null) => _stub = stub;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            if (_stub is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IStaffLookupService>();
                    services.AddSingleton(_stub);
                });
            }
        }
    }

    private sealed class StubStaffLookupService : IStaffLookupService
    {
        private readonly IReadOnlyList<StaffLookupResult> _results;

        public StubStaffLookupService(IReadOnlyList<StaffLookupResult> results) => _results = results;

        public Task<IReadOnlyList<StaffLookupResult>> LookupAsync(
            IReadOnlyList<Guid> staffIds,
            CancellationToken ct = default)
            => Task.FromResult(_results);
    }
}

[Collection(ApiCollection.Name)]
public sealed class StaffLookupEndpointTests
{
    private readonly ApiApplication _application;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public StaffLookupEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Staff_lookup_resolves_active_inactive_and_unknown_in_requested_order()
    {
        using var client = await StaffClientAsync();

        using var nurseLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiApplication.NurseEmail,
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.OK, nurseLogin.StatusCode);
        using var nurseBody = JsonDocument.Parse(await nurseLogin.Content.ReadAsStringAsync());
        var nurseId = nurseBody.RootElement.GetProperty("staff_member").GetProperty("id").GetGuid();

        var unknownId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = new[] { unknownId, nurseId }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<StaffLookupResult>>(content, JsonOptions);
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);

        Assert.Equal(unknownId, results[0].StaffId);
        Assert.False(results[0].Found);
        Assert.Null(results[0].FullName);
        Assert.Null(results[0].Role);
        Assert.Null(results[0].IsActive);

        Assert.Equal(nurseId, results[1].StaffId);
        Assert.True(results[1].Found);
        Assert.Equal("WardNurse Test", results[1].FullName);
        Assert.Equal(StaffRole.WardNurse, results[1].Role);
        Assert.True(results[1].IsActive);
    }

    [Fact]
    public async Task Staff_lookup_empty_list_returns_empty_results()
    {
        using var client = await StaffClientAsync();

        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<StaffLookupResult>>(content, JsonOptions);
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Staff_lookup_more_than_100_ids_returns_bad_request()
    {
        using var client = await StaffClientAsync();

        var tooManyIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        var response = await client.PostAsJsonAsync("/api/staff/lookup", new
        {
            staff_ids = tooManyIds
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> StaffClientAsync()
    {
        var client = _application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiApplication.NurseEmail,
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        return client;
    }
}
