using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class MyRosterAndCoverageEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/me/shifts", "get", "getMyShifts")]
    [InlineData("/me/allocations/{id}/clock-in", "post", "clockIn")]
    [InlineData("/me/allocations/{id}/clock-out", "post", "clockOut")]
    [InlineData("/coverage/wards", "get", "getWardCoverage")]
    public async Task Operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/me/shifts", "get")]
    [InlineData("/me/allocations/{id}/clock-in", "post")]
    [InlineData("/me/allocations/{id}/clock-out", "post")]
    [InlineData("/coverage/wards", "get")]
    public async Task Response_statuses_match_contract(string path, string method)
    {
        using var document = await GenerateSwaggerAsync();
        var contract = LoadContract();
        var expected = Keys(Map(contract, "paths", path, method, "responses"));
        var generated = Keys(document.RootElement, "paths", path, method, "responses");

        Assert.True(expected.SetEquals(generated),
            $"{method.ToUpperInvariant()} {path}: contract [{string.Join(", ", expected)}], "
            + $"generated [{string.Join(", ", generated)}]");
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new MyRosterTestApplication();
        using var client = app.CreateClient();

        var allocId = Guid.NewGuid();

        var getShiftsResponse = await client.GetAsync("/api/me/shifts");
        var postClockInResponse = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        var postClockOutResponse = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        var getCoverageResponse = await client.GetAsync("/api/coverage/wards");

        Assert.Equal(HttpStatusCode.Unauthorized, getShiftsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postClockInResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postClockOutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getCoverageResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_reject_patient_token_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new MyRosterTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var allocId = Guid.NewGuid();

        var getShiftsResponse = await client.GetAsync("/api/me/shifts");
        var postClockInResponse = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        var postClockOutResponse = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        var getCoverageResponse = await client.GetAsync("/api/coverage/wards");

        Assert.Equal(HttpStatusCode.Forbidden, getShiftsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postClockInResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postClockOutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getCoverageResponse.StatusCode);
    }

    [Theory]
    [InlineData("general_staff")]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("ambulance_crew")]
    [InlineData("duty_manager")]
    [InlineData("hospital_administrator")]
    [InlineData("equipment_manager")]
    public async Task Any_staff_role_can_access_endpoints(string role)
    {
        using var environment = TestEnvironment.Use();
        var stubRoster = new StubMyRosterService();
        var stubCoverage = new StubWardCoverageService();
        await using var app = new MyRosterTestApplication(stubRoster, stubCoverage);
        using var client = app.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var shiftsResponse = await client.GetAsync("/api/me/shifts");
        Assert.Equal(HttpStatusCode.OK, shiftsResponse.StatusCode);

        var coverageResponse = await client.GetAsync("/api/coverage/wards");
        Assert.Equal(HttpStatusCode.OK, coverageResponse.StatusCode);
    }

    #endregion

    #region GET /api/me/shifts Tests

    [Fact]
    public async Task Get_my_shifts_returns_caller_shifts_successfully()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var otherStaffId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddShift(callerId, new MyShiftDto
        {
            AllocationId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            WardName = "Ward A",
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "16:00",
            CrossesMidnight = false,
            Status = AllocationStatus.Confirmed,
            CanClockIn = true,
            WasReassigned = false
        });
        stubRoster.AddShift(otherStaffId, new MyShiftDto
        {
            AllocationId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            WardName = "Ward B",
            Date = new DateOnly(2026, 10, 1),
            StartTime = "16:00",
            EndTime = "00:00",
            CrossesMidnight = false,
            Status = AllocationStatus.Confirmed,
            CanClockIn = false,
            WasReassigned = false
        });

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.GetAsync("/api/me/shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shifts = await response.Content.ReadFromJsonAsync<List<MyShiftDto>>(JsonOptions);
        Assert.NotNull(shifts);
        Assert.Single(shifts);
        Assert.Equal("Ward A", shifts[0].WardName);
        Assert.True(shifts[0].CanClockIn);
    }

    [Fact]
    public async Task Get_my_shifts_validates_date_range()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new MyRosterTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        // to < from should fail validation
        var response = await client.GetAsync("/api/me/shifts?from=2026-10-10&to=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_my_shifts_passes_query_parameters_to_service()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var stubRoster = new StubMyRosterService();

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.GetAsync("/api/me/shifts?from=2026-10-01&to=2026-10-15&includePast=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(stubRoster.LastParameters);
        Assert.Equal(new DateOnly(2026, 10, 1), stubRoster.LastParameters.From);
        Assert.Equal(new DateOnly(2026, 10, 15), stubRoster.LastParameters.To);
        Assert.True(stubRoster.LastParameters.IncludePast);
    }

    #endregion

    #region POST /api/me/allocations/{id}/clock-in Tests

    [Fact]
    public async Task Clock_in_returns_200_and_sets_clocked_in_at()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Confirmed);

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AllocationDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.NotNull(dto.ClockedInAt);
        Assert.Null(dto.ClockedOutAt);
    }

    [Fact]
    public async Task Clock_in_returns_404_when_allocation_not_found()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new MyRosterTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsync($"/api/me/allocations/{Guid.NewGuid()}/clock-in", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clock_in_returns_403_when_allocation_belongs_to_another_staff_member()
    {
        using var environment = TestEnvironment.Use();
        var otherStaffId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(otherStaffId, allocId, AllocationStatus.Confirmed);

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clock_in_returns_409_when_already_clocked_in()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Confirmed, clockedInAt: DateTimeOffset.UtcNow);

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Clock_in_returns_409_when_outside_permitted_window()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Confirmed, isOutsideWindow: true);

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Clock_in_returns_409_when_allocation_is_not_confirmed()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Released);

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-in", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    #endregion

    #region POST /api/me/allocations/{id}/clock-out Tests

    [Fact]
    public async Task Clock_out_returns_200_and_sets_clocked_out_at()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Confirmed, clockedInAt: DateTimeOffset.UtcNow.AddHours(-4));

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AllocationDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.NotNull(dto.ClockedInAt);
        Assert.NotNull(dto.ClockedOutAt);
    }

    [Fact]
    public async Task Clock_out_returns_404_when_allocation_not_found()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new MyRosterTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsync($"/api/me/allocations/{Guid.NewGuid()}/clock-out", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clock_out_returns_403_when_allocation_belongs_to_another_staff_member()
    {
        using var environment = TestEnvironment.Use();
        var otherStaffId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(otherStaffId, allocId, AllocationStatus.Confirmed, clockedInAt: DateTimeOffset.UtcNow.AddHours(-1));

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clock_out_returns_409_when_not_clocked_in()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Confirmed, clockedInAt: null);

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Clock_out_returns_409_when_already_clocked_out()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Confirmed,
            clockedInAt: DateTimeOffset.UtcNow.AddHours(-4),
            clockedOutAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    #endregion

    #region GET /api/coverage/wards Tests

    [Fact]
    public async Task Get_ward_coverage_returns_counts_only_without_staff_identities()
    {
        using var environment = TestEnvironment.Use();
        var stubCoverage = new StubWardCoverageService();
        var wardId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        stubCoverage.SetResponse(new WardCoverageOverviewResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Wards = new List<WardCoverageDto>
            {
                new()
                {
                    WardId = wardId,
                    WardName = "Cardiology Ward",
                    CurrentShiftId = shiftId,
                    OnDutyCount = 4,
                    MinimumHeadcount = 3,
                    HeadcountNeeded = 5,
                    Status = CoverageStatus.AtMinimum,
                    ByRole = new Dictionary<string, int>
                    {
                        ["ward_nurse"] = 3,
                        ["doctor"] = 1
                    }
                }
            }
        });

        await using var app = new MyRosterTestApplication(null, stubCoverage);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("doctor", "staff"));

        var response = await client.GetAsync("/api/coverage/wards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        // Crucial requirement: No staff identities cross this boundary
        Assert.DoesNotContain("staff_id", body);
        Assert.DoesNotContain("staff_member_id", body);
        Assert.DoesNotContain("full_name", body);
        Assert.DoesNotContain("first_name", body);

        var overview = await response.Content.ReadFromJsonAsync<WardCoverageOverviewResponse>(JsonOptions);
        Assert.NotNull(overview);
        Assert.Single(overview.Wards);

        var ward = overview.Wards[0];
        Assert.Equal(wardId, ward.WardId);
        Assert.Equal("Cardiology Ward", ward.WardName);
        Assert.Equal(4, ward.OnDutyCount);
        Assert.Equal(CoverageStatus.AtMinimum, ward.Status);
        Assert.Equal(3, ward.ByRole["ward_nurse"]);
        Assert.Equal(1, ward.ByRole["doctor"]);
    }

    [Fact]
    public async Task Get_ward_coverage_passes_at_timestamp_parameter()
    {
        using var environment = TestEnvironment.Use();
        var stubCoverage = new StubWardCoverageService();
        await using var app = new MyRosterTestApplication(null, stubCoverage);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var timestamp = "2026-10-05T14:30:00Z";
        var response = await client.GetAsync($"/api/coverage/wards?at={Uri.EscapeDataString(timestamp)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(stubCoverage.LastAt);
        Assert.Equal(DateTimeOffset.Parse(timestamp), stubCoverage.LastAt.Value);
    }

    [Fact]
    public async Task Get_my_shifts_returns_empty_when_no_shifts()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var stubRoster = new StubMyRosterService();

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.GetAsync("/api/me/shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shifts = await response.Content.ReadFromJsonAsync<List<MyShiftDto>>(JsonOptions);
        Assert.NotNull(shifts);
        Assert.Empty(shifts);
    }

    [Fact]
    public async Task Get_my_shifts_reflects_was_reassigned_and_crosses_midnight()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var stubRoster = new StubMyRosterService();
        stubRoster.AddShift(callerId, new MyShiftDto
        {
            AllocationId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            WardName = "ICU",
            Date = new DateOnly(2026, 10, 2),
            StartTime = "22:00",
            EndTime = "06:00",
            CrossesMidnight = true,
            Status = AllocationStatus.Confirmed,
            CanClockIn = false,
            WasReassigned = true
        });

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.GetAsync("/api/me/shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shifts = await response.Content.ReadFromJsonAsync<List<MyShiftDto>>(JsonOptions);
        Assert.NotNull(shifts);
        Assert.Single(shifts);
        Assert.True(shifts[0].CrossesMidnight);
        Assert.True(shifts[0].WasReassigned);
        Assert.False(shifts[0].CanClockIn);
    }

    [Fact]
    public async Task Clock_out_returns_409_when_allocation_is_released()
    {
        using var environment = TestEnvironment.Use();
        var callerId = Guid.NewGuid();
        var allocId = Guid.NewGuid();

        var stubRoster = new StubMyRosterService();
        stubRoster.AddAllocation(callerId, allocId, AllocationStatus.Released, clockedInAt: DateTimeOffset.UtcNow.AddHours(-2));

        await using var app = new MyRosterTestApplication(stubRoster);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", callerId));

        var response = await client.PostAsync($"/api/me/allocations/{allocId}/clock-out", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_ward_coverage_reflects_understaffed_and_critical_statuses()
    {
        using var environment = TestEnvironment.Use();
        var stubCoverage = new StubWardCoverageService();
        var ward1 = Guid.NewGuid();
        var ward2 = Guid.NewGuid();

        stubCoverage.SetResponse(new WardCoverageOverviewResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Wards = new List<WardCoverageDto>
            {
                new()
                {
                    WardId = ward1,
                    WardName = "Ward Understaffed",
                    OnDutyCount = 1,
                    MinimumHeadcount = 3,
                    HeadcountNeeded = 4,
                    Status = CoverageStatus.Understaffed,
                    ByRole = new Dictionary<string, int> { ["ward_nurse"] = 1 }
                },
                new()
                {
                    WardId = ward2,
                    WardName = "Ward Critical",
                    OnDutyCount = 0,
                    MinimumHeadcount = 2,
                    HeadcountNeeded = 3,
                    Status = CoverageStatus.Critical,
                    ByRole = new Dictionary<string, int>()
                }
            }
        });

        await using var app = new MyRosterTestApplication(null, stubCoverage);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("doctor", "staff"));

        var response = await client.GetAsync("/api/coverage/wards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var overview = await response.Content.ReadFromJsonAsync<WardCoverageOverviewResponse>(JsonOptions);
        Assert.NotNull(overview);
        Assert.Equal(2, overview.Wards.Count);
        Assert.Equal(CoverageStatus.Understaffed, overview.Wards[0].Status);
        Assert.Equal(CoverageStatus.Critical, overview.Wards[1].Status);
        Assert.Empty(overview.Wards[1].ByRole);
    }

    #endregion

    #region Helpers and Test Doubles

    private static string CreateToken(string role, string principalType, Guid? subject = null)
    {
        var claims = new[]
        {
            new Claim(CareLankaClaims.Subject, (subject ?? Guid.NewGuid()).ToString()),
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

    private static async Task<JsonDocument> GenerateSwaggerAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static YamlMappingNode LoadContract()
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "../../../../../specs/staff-spec.yaml");
        if (!File.Exists(specPath))
        {
            specPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "specs/staff-spec.yaml"));
        }
        var yaml = new YamlStream();
        using var reader = new StreamReader(specPath);
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static ISet<string> Keys(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current.EnumerateObject().Select(property => property.Name).ToHashSet();
    }

    private static YamlMappingNode Map(YamlMappingNode node, params string[] path)
    {
        var current = node;
        foreach (var segment in path)
        {
            current = (YamlMappingNode)current.Children[new YamlScalarNode(segment)];
        }

        return current;
    }

    private static ISet<string> Keys(YamlMappingNode node)
        => node.Children.Keys.Select(key => ((YamlScalarNode)key).Value!).ToHashSet();

    private sealed class SwaggerOnlyApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }

    private sealed class MyRosterTestApplication : WebApplicationFactory<Program>
    {
        private readonly StubMyRosterService _stubRoster;
        private readonly StubWardCoverageService _stubCoverage;

        public MyRosterTestApplication(
            StubMyRosterService? stubRoster = null,
            StubWardCoverageService? stubCoverage = null)
        {
            _stubRoster = stubRoster ?? new StubMyRosterService();
            _stubCoverage = stubCoverage ?? new StubWardCoverageService();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMyRosterService>();
                services.AddScoped<IMyRosterService>(sp =>
                {
                    _stubRoster.Accessor = sp.GetRequiredService<IHttpContextAccessor>();
                    return _stubRoster;
                });

                services.RemoveAll<IWardCoverageService>();
                services.AddScoped<IWardCoverageService>(_ => _stubCoverage);
            });
        }
    }

    public sealed class StubMyRosterService : IMyRosterService
    {
        public IHttpContextAccessor? Accessor { get; set; }
        public GetMyShiftsParameters? LastParameters { get; private set; }

        private readonly Dictionary<Guid, List<MyShiftDto>> _shifts = new();
        private readonly Dictionary<Guid, (Guid StaffMemberId, AllocationStatus Status, DateTimeOffset? ClockedInAt, DateTimeOffset? ClockedOutAt, bool IsOutsideWindow)> _allocations = new();

        private Guid CallerId
        {
            get
            {
                var sub = Accessor?.HttpContext?.User.FindFirst(CareLankaClaims.Subject)?.Value;
                return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
            }
        }

        public void AddShift(Guid staffId, MyShiftDto shift)
        {
            if (!_shifts.TryGetValue(staffId, out var list))
            {
                list = new List<MyShiftDto>();
                _shifts[staffId] = list;
            }
            list.Add(shift);
        }

        public void AddAllocation(
            Guid staffMemberId,
            Guid allocationId,
            AllocationStatus status,
            DateTimeOffset? clockedInAt = null,
            DateTimeOffset? clockedOutAt = null,
            bool isOutsideWindow = false)
        {
            _allocations[allocationId] = (staffMemberId, status, clockedInAt, clockedOutAt, isOutsideWindow);
        }

        public Task<IReadOnlyList<MyShiftDto>> GetMyShiftsAsync(
            GetMyShiftsParameters parameters,
            CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            var list = _shifts.TryGetValue(CallerId, out var shifts)
                ? (IReadOnlyList<MyShiftDto>)shifts.ToList()
                : Array.Empty<MyShiftDto>();
            return Task.FromResult(list);
        }

        public Task<AllocationDto> ClockInAsync(
            Guid allocationId,
            CancellationToken cancellationToken = default)
        {
            if (!_allocations.TryGetValue(allocationId, out var alloc))
            {
                throw new NotFoundException("Allocation", allocationId);
            }

            if (alloc.StaffMemberId != CallerId)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            if (alloc.Status != AllocationStatus.Confirmed)
            {
                throw new ConflictException(MessageCode.Conflict, "Allocation is not confirmed.");
            }

            if (alloc.ClockedInAt != null)
            {
                throw new ConflictException(MessageCode.Conflict, "Already clocked in.");
            }

            if (alloc.IsOutsideWindow)
            {
                throw new ConflictException(MessageCode.Conflict, "Outside the permitted clock-in window.");
            }

            var now = DateTimeOffset.UtcNow;
            _allocations[allocationId] = (alloc.StaffMemberId, alloc.Status, now, alloc.ClockedOutAt, alloc.IsOutsideWindow);

            return Task.FromResult(new AllocationDto
            {
                Id = allocationId,
                ShiftId = Guid.NewGuid(),
                StaffMemberId = alloc.StaffMemberId,
                StaffName = "Test Staff",
                Status = alloc.Status,
                Source = AllocationSource.Manual,
                ClockedInAt = now,
                ClockedOutAt = alloc.ClockedOutAt,
                CreatedAt = now
            });
        }

        public Task<AllocationDto> ClockOutAsync(
            Guid allocationId,
            CancellationToken cancellationToken = default)
        {
            if (!_allocations.TryGetValue(allocationId, out var alloc))
            {
                throw new NotFoundException("Allocation", allocationId);
            }

            if (alloc.StaffMemberId != CallerId)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            if (alloc.Status != AllocationStatus.Confirmed)
            {
                throw new ConflictException(MessageCode.Conflict, "Allocation is not confirmed.");
            }

            if (alloc.ClockedInAt == null)
            {
                throw new ConflictException(MessageCode.Conflict, "Not clocked in.");
            }

            if (alloc.ClockedOutAt != null)
            {
                throw new ConflictException(MessageCode.Conflict, "Already clocked out.");
            }

            var now = DateTimeOffset.UtcNow;
            _allocations[allocationId] = (alloc.StaffMemberId, alloc.Status, alloc.ClockedInAt, now, alloc.IsOutsideWindow);

            return Task.FromResult(new AllocationDto
            {
                Id = allocationId,
                ShiftId = Guid.NewGuid(),
                StaffMemberId = alloc.StaffMemberId,
                StaffName = "Test Staff",
                Status = alloc.Status,
                Source = AllocationSource.Manual,
                ClockedInAt = alloc.ClockedInAt,
                ClockedOutAt = now,
                CreatedAt = now
            });
        }
    }

    public sealed class StubWardCoverageService : IWardCoverageService
    {
        public DateTimeOffset? LastAt { get; private set; }
        private WardCoverageOverviewResponse _response = new()
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Wards = Array.Empty<WardCoverageDto>()
        };

        public void SetResponse(WardCoverageOverviewResponse response) => _response = response;

        public Task<WardCoverageOverviewResponse> GetWardCoverageAsync(
            DateTimeOffset? at,
            CancellationToken cancellationToken = default)
        {
            LastAt = at;
            return Task.FromResult(_response);
        }
    }

    #endregion
}
