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
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class ShiftEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/shifts", "get", "listShifts")]
    [InlineData("/shifts", "post", "createShift")]
    [InlineData("/shifts/bulk", "post", "createShiftsBulk")]
    [InlineData("/shifts/{id}", "get", "getShift")]
    [InlineData("/shifts/{id}", "put", "updateShift")]
    [InlineData("/shifts/{id}", "delete", "cancelShift")]
    [InlineData("/wards/{wardId}/staffing-rules", "get", "getWardStaffingRules")]
    [InlineData("/wards/{wardId}/staffing-rules", "put", "setWardStaffingRules")]
    public async Task Shift_operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/shifts", "get")]
    [InlineData("/shifts", "post")]
    [InlineData("/shifts/bulk", "post")]
    [InlineData("/shifts/{id}", "get")]
    [InlineData("/shifts/{id}", "put")]
    [InlineData("/shifts/{id}", "delete")]
    [InlineData("/wards/{wardId}/staffing-rules", "get")]
    [InlineData("/wards/{wardId}/staffing-rules", "put")]
    public async Task Shift_response_statuses_match_contract(string path, string method)
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
    public async Task Shifts_endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();

        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();

        var r1 = await client.GetAsync($"/api/shifts?from=2026-10-01&to=2026-10-07");
        var r2 = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest(), JsonOptions);
        var r3 = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest(), JsonOptions);
        var r4 = await client.GetAsync($"/api/shifts/{shiftId}");
        var r5 = await client.PutAsJsonAsync($"/api/shifts/{shiftId}", new CreateShiftRequest(), JsonOptions);
        var r6 = await client.DeleteAsync($"/api/shifts/{shiftId}");
        var r7 = await client.GetAsync($"/api/wards/{wardId}/staffing-rules");
        var r8 = await client.PutAsJsonAsync($"/api/wards/{wardId}/staffing-rules", new List<WardStaffingRuleInput>(), JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r3.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r4.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r5.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r6.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r7.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r8.StatusCode);
    }

    [Fact]
    public async Task Duty_manager_can_read_shifts_and_staffing_rules_but_cannot_modify()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();

        stub.AddShift(new ShiftDetailDto
        {
            Id = shiftId,
            WardId = wardId,
            WardName = "ICU",
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "16:00",
            CrossesMidnight = false,
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 3,
            MinimumHeadcount = 2,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Coverage = new ShiftCoverageDto
            {
                ConfirmedCount = 2,
                HeadcountNeeded = 3,
                MinimumHeadcount = 2,
                Status = CoverageStatus.AtMinimum,
                ShortfallToMinimum = 0
            }
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var getShifts = await client.GetAsync("/api/shifts?from=2026-10-01&to=2026-10-07");
        var getShift = await client.GetAsync($"/api/shifts/{shiftId}");
        var getRules = await client.GetAsync($"/api/wards/{wardId}/staffing-rules");

        Assert.Equal(HttpStatusCode.OK, getShifts.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getShift.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getRules.StatusCode);

        var postShift = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = wardId,
            Date = new DateOnly(2026, 10, 2),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 2
        }, JsonOptions);
        var postBulk = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest
        {
            WardId = wardId,
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 5),
            Patterns = new List<BulkShiftPatternItem>
            {
                new() { StartTime = "08:00", EndTime = "16:00", RequiredRole = StaffRole.WardNurse, HeadcountNeeded = 2 }
            }
        }, JsonOptions);
        var putShift = await client.PutAsJsonAsync($"/api/shifts/{shiftId}", new CreateShiftRequest
        {
            WardId = wardId,
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 4
        }, JsonOptions);
        var deleteShift = await client.DeleteAsync($"/api/shifts/{shiftId}");
        var putRules = await client.PutAsJsonAsync($"/api/wards/{wardId}/staffing-rules", new List<WardStaffingRuleInput>
        {
            new() { RequiredRole = StaffRole.WardNurse, MinimumHeadcount = 2 }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, postShift.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postBulk.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putShift.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteShift.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, putRules.StatusCode);
    }

    [Fact]
    public async Task Hospital_administrator_allowed_for_all_shift_endpoints()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();

        stub.AddShift(new ShiftDetailDto
        {
            Id = shiftId,
            WardId = wardId,
            WardName = "Emergency",
            Date = new DateOnly(2026, 10, 5),
            StartTime = "22:00",
            EndTime = "06:00",
            CrossesMidnight = true,
            RequiredRole = StaffRole.Doctor,
            HeadcountNeeded = 2,
            MinimumHeadcount = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Coverage = new ShiftCoverageDto
            {
                ConfirmedCount = 2,
                HeadcountNeeded = 2,
                MinimumHeadcount = 1,
                Status = CoverageStatus.Adequate,
                ShortfallToMinimum = 0
            }
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var getShifts = await client.GetAsync("/api/shifts?from=2026-10-01&to=2026-10-10");
        Assert.Equal(HttpStatusCode.OK, getShifts.StatusCode);

        var createShift = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = wardId,
            Date = new DateOnly(2026, 10, 6),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.Doctor,
            HeadcountNeeded = 2
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createShift.StatusCode);

        var createBulk = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest
        {
            WardId = wardId,
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 3),
            Patterns = new List<BulkShiftPatternItem>
            {
                new() { StartTime = "08:00", EndTime = "16:00", RequiredRole = StaffRole.Doctor, HeadcountNeeded = 1 }
            }
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createBulk.StatusCode);

        var getShift = await client.GetAsync($"/api/shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.OK, getShift.StatusCode);

        var updateShift = await client.PutAsJsonAsync($"/api/shifts/{shiftId}", new CreateShiftRequest
        {
            WardId = wardId,
            Date = new DateOnly(2026, 10, 5),
            StartTime = "22:00",
            EndTime = "06:00",
            RequiredRole = StaffRole.Doctor,
            HeadcountNeeded = 3
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, updateShift.StatusCode);

        var deleteShift = await client.DeleteAsync($"/api/shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteShift.StatusCode);

        var getRules = await client.GetAsync($"/api/wards/{wardId}/staffing-rules");
        Assert.Equal(HttpStatusCode.OK, getRules.StatusCode);

        var putRules = await client.PutAsJsonAsync($"/api/wards/{wardId}/staffing-rules", new List<WardStaffingRuleInput>
        {
            new() { RequiredRole = StaffRole.Doctor, MinimumHeadcount = 2 }
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, putRules.StatusCode);
    }

    [Fact]
    public async Task Patient_token_is_forbidden_on_all_shift_endpoints()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var response = await client.GetAsync("/api/shifts?from=2026-10-01&to=2026-10-05");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Request Validation Tests

    [Fact]
    public async Task Create_shift_rejects_missing_required_fields()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest(), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("25:00", "08:00")]
    [InlineData("08:00", "24:00")]
    [InlineData("8:00", "16:00")]
    [InlineData("08:60", "16:00")]
    [InlineData("invalid", "16:00")]
    public async Task Create_shift_rejects_invalid_time_format(string startTime, string endTime)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 1),
            StartTime = startTime,
            EndTime = endTime,
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 2
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_shift_rejects_identical_start_and_end_time()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "08:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 2
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_shift_rejects_minimum_headcount_greater_than_needed()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 2,
            MinimumHeadcount = 3
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_shift_rejects_to_date_before_from_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest
        {
            WardId = Guid.NewGuid(),
            From = new DateOnly(2026, 10, 5),
            To = new DateOnly(2026, 10, 1),
            Patterns = new List<BulkShiftPatternItem>
            {
                new() { StartTime = "08:00", EndTime = "16:00", RequiredRole = StaffRole.WardNurse, HeadcountNeeded = 2 }
            }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_shift_rejects_empty_patterns()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest
        {
            WardId = Guid.NewGuid(),
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 5),
            Patterns = new List<BulkShiftPatternItem>()
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_shift_rejects_invalid_weekday_abbreviation()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest
        {
            WardId = Guid.NewGuid(),
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 5),
            Weekdays = new List<string> { "mon", "tuesday", "fri" },
            Patterns = new List<BulkShiftPatternItem>
            {
                new() { StartTime = "08:00", EndTime = "16:00", RequiredRole = StaffRole.WardNurse, HeadcountNeeded = 2 }
            }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_shifts_rejects_missing_from_or_to_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/shifts");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_shifts_rejects_to_before_from()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync("/api/shifts?from=2026-10-10&to=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("page=1&pageSize=0")]
    [InlineData("page=1&pageSize=101")]
    [InlineData("from=2026-10-01&to=2026-10-05&sortBy=invalid")]
    [InlineData("from=2026-10-01&to=2026-10-05&sortDir=invalid")]
    public async Task List_shifts_rejects_invalid_pagination_or_sorting(string query)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/shifts?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ward_staffing_rules_rejects_minimum_headcount_less_than_1()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new ShiftTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PutAsJsonAsync($"/api/wards/{Guid.NewGuid()}/staffing-rules", new List<WardStaffingRuleInput>
        {
            new() { RequiredRole = StaffRole.WardNurse, MinimumHeadcount = 0 }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Business Logic & Edge Cases

    [Fact]
    public async Task Create_shift_calculates_overnight_crosses_midnight_correctly()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        // Overnight shift: 22:00 to 06:00
        var overnightResponse = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 1),
            StartTime = "22:00",
            EndTime = "06:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 3
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, overnightResponse.StatusCode);
        var overnightShift = await overnightResponse.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);
        Assert.NotNull(overnightShift);
        Assert.True(overnightShift.CrossesMidnight);

        // Day shift: 08:00 to 16:00
        var dayResponse = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 2),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 3
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, dayResponse.StatusCode);
        var dayShift = await dayResponse.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);
        Assert.NotNull(dayShift);
        Assert.False(dayShift.CrossesMidnight);
    }

    [Fact]
    public async Task Create_shift_derives_minimum_headcount_from_ward_staffing_rules_when_omitted()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var wardId = Guid.NewGuid();
        var skillId = Guid.NewGuid();

        stub.SetWardStaffingRules(wardId, new List<WardStaffingRuleDto>
        {
            new() { Id = Guid.NewGuid(), WardId = wardId, RequiredRole = StaffRole.WardNurse, RequiredSkillId = skillId, MinimumHeadcount = 2 }
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        // When minimum_headcount is omitted, it should derive 2 from the rule
        var response = await client.PostAsJsonAsync("/api/shifts", new CreateShiftRequest
        {
            WardId = wardId,
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            RequiredSkillId = skillId,
            HeadcountNeeded = 5,
            MinimumHeadcount = null
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var shift = await response.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions);
        Assert.NotNull(shift);
        Assert.Equal(2, shift.MinimumHeadcount);
    }

    [Fact]
    public async Task Bulk_shift_creation_skips_duplicates_and_returns_summary()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var wardId = Guid.NewGuid();

        // Pre-existing shift on 2026-10-01 08:00-16:00 for WardNurse
        stub.AddShift(new ShiftDetailDto
        {
            Id = Guid.NewGuid(),
            WardId = wardId,
            WardName = "ICU",
            Date = new DateOnly(2026, 10, 1),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 2,
            MinimumHeadcount = 1
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/shifts/bulk", new BulkShiftRequest
        {
            WardId = wardId,
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 2),
            Weekdays = new List<string> { "thu", "fri" },
            Patterns = new List<BulkShiftPatternItem>
            {
                new() { StartTime = "08:00", EndTime = "16:00", RequiredRole = StaffRole.WardNurse, HeadcountNeeded = 2 }
            }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkShiftResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.Skipped); // 2026-10-01 was skipped
        Assert.Equal(1, result.Created); // 2026-10-02 was created
    }

    [Fact]
    public async Task Get_shift_returns_allocations_and_coverage()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        stub.AddShift(new ShiftDetailDto
        {
            Id = shiftId,
            WardId = wardId,
            WardName = "Maternity",
            Date = new DateOnly(2026, 10, 15),
            StartTime = "08:00",
            EndTime = "16:00",
            CrossesMidnight = false,
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 4,
            MinimumHeadcount = 2,
            Coverage = new ShiftCoverageDto
            {
                ConfirmedCount = 2,
                HeadcountNeeded = 4,
                MinimumHeadcount = 2,
                Status = CoverageStatus.AtMinimum,
                ShortfallToMinimum = 0
            },
            Allocations = new List<AllocationDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ShiftId = shiftId,
                    StaffMemberId = staffId,
                    StaffName = "Sister Silva",
                    Status = AllocationStatus.Confirmed,
                    Source = AllocationSource.Manual,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            }
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<ShiftDetailDto>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(shiftId, detail.Id);
        Assert.Equal("Maternity", detail.WardName);
        Assert.Single(detail.Allocations);
        Assert.Equal("Sister Silva", detail.Allocations[0].StaffName);
        Assert.Equal(CoverageStatus.AtMinimum, detail.Coverage.Status);
    }

    [Fact]
    public async Task Get_shift_returns_404_when_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/shifts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_shift_releases_allocations_for_future_shift()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();

        stub.AddShift(new ShiftDetailDto
        {
            Id = shiftId,
            WardId = wardId,
            WardName = "Surgical",
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.Doctor,
            HeadcountNeeded = 2,
            MinimumHeadcount = 1,
            Allocations = new List<AllocationDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ShiftId = shiftId,
                    StaffMemberId = Guid.NewGuid(),
                    StaffName = "Dr. Perera",
                    Status = AllocationStatus.Confirmed,
                    Source = AllocationSource.Manual,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            }
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.DeleteAsync($"/api/shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var shift = stub.GetShift(shiftId);
        Assert.NotNull(shift);
        Assert.Equal(AllocationStatus.Released, shift.Allocations[0].Status);
        Assert.Equal(AllocationEndReason.ShiftCancelled, shift.Allocations[0].EndedReason);
    }

    [Fact]
    public async Task Cancel_shift_returns_409_when_shift_already_started()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();

        // Shift started yesterday
        stub.AddShift(new ShiftDetailDto
        {
            Id = shiftId,
            WardId = wardId,
            WardName = "Surgical",
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.Doctor,
            HeadcountNeeded = 2,
            MinimumHeadcount = 1
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.DeleteAsync($"/api/shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Replace_ward_staffing_rules_identifies_disagreeing_shifts()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubShiftService();
        var wardId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        // Existing shift with minimum_headcount = 1
        stub.AddShift(new ShiftDetailDto
        {
            Id = Guid.NewGuid(),
            WardId = wardId,
            WardName = "Pediatrics",
            Date = futureDate,
            StartTime = "08:00",
            EndTime = "16:00",
            RequiredRole = StaffRole.WardNurse,
            HeadcountNeeded = 4,
            MinimumHeadcount = 1
        });

        await using var app = new ShiftTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        // Setting new rule: WardNurse minimum is now 3
        var response = await client.PutAsJsonAsync($"/api/wards/{wardId}/staffing-rules", new List<WardStaffingRuleInput>
        {
            new() { RequiredRole = StaffRole.WardNurse, MinimumHeadcount = 3 }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReplaceWardStaffingRulesResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Rules);
        Assert.Equal(3, result.Rules[0].MinimumHeadcount);
        Assert.Single(result.ShiftsNowDisagreeing); // The shift with minimum=1 now disagrees with rule minimum=3
    }

    [Theory]
    [InlineData(4, 4, 2, CoverageStatus.Adequate)]
    [InlineData(3, 4, 2, CoverageStatus.AtMinimum)]
    [InlineData(2, 4, 2, CoverageStatus.AtMinimum)]
    [InlineData(1, 4, 2, CoverageStatus.Understaffed)]
    [InlineData(0, 4, 2, CoverageStatus.Critical)]
    public void Coverage_calculation_derives_correct_status(
        int confirmed,
        int needed,
        int min,
        CoverageStatus expectedStatus)
    {
        var coverage = ComputeCoverageTestHelper(confirmed, needed, min);
        Assert.Equal(expectedStatus, coverage.Status);
        Assert.Equal(Math.Max(0, min - confirmed), coverage.ShortfallToMinimum);
    }

    #endregion

    #region Helper Methods & Stubs

    private static ShiftCoverageDto ComputeCoverageTestHelper(int confirmedCount, int headcountNeeded, int minimumHeadcount)
    {
        var shortfall = Math.Max(0, minimumHeadcount - confirmedCount);
        CoverageStatus status;
        if (confirmedCount >= headcountNeeded)
        {
            status = CoverageStatus.Adequate;
        }
        else if (confirmedCount >= minimumHeadcount)
        {
            status = CoverageStatus.AtMinimum;
        }
        else if (confirmedCount > 0)
        {
            status = CoverageStatus.Understaffed;
        }
        else
        {
            status = CoverageStatus.Critical;
        }

        return new ShiftCoverageDto
        {
            ConfirmedCount = confirmedCount,
            HeadcountNeeded = headcountNeeded,
            MinimumHeadcount = minimumHeadcount,
            Status = status,
            ShortfallToMinimum = shortfall
        };
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
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IShiftService, ShiftService>();
            });
        }
    }

    private sealed class ShiftTestApplication : WebApplicationFactory<Program>
    {
        private readonly IShiftService? _stub;

        public ShiftTestApplication(IShiftService? stub = null)
        {
            _stub = stub;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IShiftService, ShiftService>();
                if (_stub != null)
                {
                    services.RemoveAll<IShiftService>();
                    services.AddSingleton(_stub);
                }
            });
        }
    }

    private sealed class StubShiftService : IShiftService
    {
        private readonly List<ShiftDetailDto> _shifts = new();
        private readonly Dictionary<Guid, List<WardStaffingRuleDto>> _wardRules = new();

        public void AddShift(ShiftDetailDto shift) => _shifts.Add(shift);
        public ShiftDetailDto? GetShift(Guid id) => _shifts.FirstOrDefault(s => s.Id == id);
        public void SetWardStaffingRules(Guid wardId, List<WardStaffingRuleDto> rules) => _wardRules[wardId] = rules;

        public Task<PagedResult<ShiftSummaryDto>> ListShiftsAsync(ListShiftsQueryParameters parameters, CancellationToken ct = default)
        {
            if (parameters.To < parameters.From)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "To date must be >= From date.");
            }

            var query = _shifts.AsEnumerable();
            if (parameters.WardId.HasValue)
            {
                query = query.Where(s => s.WardId == parameters.WardId.Value);
            }

            if (parameters.Role.HasValue)
            {
                query = query.Where(s => s.RequiredRole == parameters.Role.Value);
            }

            var summaries = query.Select(s => new ShiftSummaryDto
            {
                Id = s.Id,
                WardId = s.WardId,
                WardName = s.WardName,
                Date = s.Date,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                CrossesMidnight = s.CrossesMidnight,
                RequiredRole = s.RequiredRole,
                RequiredSkillId = s.RequiredSkillId,
                RequiredSkillName = s.RequiredSkillName,
                HeadcountNeeded = s.HeadcountNeeded,
                MinimumHeadcount = s.MinimumHeadcount,
                Coverage = s.Coverage,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            }).ToList();

            var paged = PagedResult<ShiftSummaryDto>.From(summaries, parameters.Page, parameters.PageSize, summaries.Count);
            return Task.FromResult(paged);
        }

        public Task<ShiftSummaryDto> CreateShiftAsync(CreateShiftRequest request, CancellationToken ct = default)
        {
            var start = TimeOnly.Parse(request.StartTime);
            var end = TimeOnly.Parse(request.EndTime);
            var crossesMidnight = end < start;

            int minHeadcount = request.MinimumHeadcount ?? request.HeadcountNeeded;
            if (!request.MinimumHeadcount.HasValue && _wardRules.TryGetValue(request.WardId, out var rules))
            {
                var rule = rules.FirstOrDefault(r => r.RequiredRole == request.RequiredRole && r.RequiredSkillId == request.RequiredSkillId)
                    ?? rules.FirstOrDefault(r => r.RequiredRole == request.RequiredRole && r.RequiredSkillId == null);
                if (rule != null)
                {
                    minHeadcount = rule.MinimumHeadcount;
                }
            }

            var shift = new ShiftDetailDto
            {
                Id = Guid.NewGuid(),
                WardId = request.WardId,
                WardName = "Ward",
                Date = request.Date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                CrossesMidnight = crossesMidnight,
                RequiredRole = request.RequiredRole,
                RequiredSkillId = request.RequiredSkillId,
                HeadcountNeeded = request.HeadcountNeeded,
                MinimumHeadcount = minHeadcount,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Coverage = new ShiftCoverageDto
                {
                    ConfirmedCount = 0,
                    HeadcountNeeded = request.HeadcountNeeded,
                    MinimumHeadcount = minHeadcount,
                    Status = CoverageStatus.Critical,
                    ShortfallToMinimum = minHeadcount
                }
            };
            _shifts.Add(shift);

            return Task.FromResult(new ShiftSummaryDto
            {
                Id = shift.Id,
                WardId = shift.WardId,
                WardName = shift.WardName,
                Date = shift.Date,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                CrossesMidnight = shift.CrossesMidnight,
                RequiredRole = shift.RequiredRole,
                RequiredSkillId = shift.RequiredSkillId,
                HeadcountNeeded = shift.HeadcountNeeded,
                MinimumHeadcount = shift.MinimumHeadcount,
                Coverage = shift.Coverage,
                CreatedAt = shift.CreatedAt,
                UpdatedAt = shift.UpdatedAt
            });
        }

        public Task<BulkShiftResponse> CreateShiftsBulkAsync(BulkShiftRequest request, CancellationToken ct = default)
        {
            int created = 0;
            int skipped = 0;
            var createdDtos = new List<ShiftSummaryDto>();

            var existingKeys = _shifts
                .Where(s => s.WardId == request.WardId && s.Date >= request.From && s.Date <= request.To)
                .Select(s => (s.Date, s.StartTime, s.EndTime, s.RequiredRole))
                .ToHashSet();

            for (var d = request.From; d <= request.To; d = d.AddDays(1))
            {
                foreach (var p in request.Patterns)
                {
                    var key = (d, p.StartTime, p.EndTime, p.RequiredRole);
                    if (existingKeys.Contains(key))
                    {
                        skipped++;
                        continue;
                    }

                    var shift = new ShiftDetailDto
                    {
                        Id = Guid.NewGuid(),
                        WardId = request.WardId,
                        WardName = "Ward",
                        Date = d,
                        StartTime = p.StartTime,
                        EndTime = p.EndTime,
                        CrossesMidnight = TimeOnly.Parse(p.EndTime) < TimeOnly.Parse(p.StartTime),
                        RequiredRole = p.RequiredRole,
                        RequiredSkillId = p.RequiredSkillId,
                        HeadcountNeeded = p.HeadcountNeeded,
                        MinimumHeadcount = p.MinimumHeadcount ?? p.HeadcountNeeded,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _shifts.Add(shift);
                    existingKeys.Add(key);
                    created++;
                    createdDtos.Add(new ShiftSummaryDto
                    {
                        Id = shift.Id,
                        WardId = shift.WardId,
                        WardName = shift.WardName,
                        Date = shift.Date,
                        StartTime = shift.StartTime,
                        EndTime = shift.EndTime,
                        CrossesMidnight = shift.CrossesMidnight,
                        RequiredRole = shift.RequiredRole,
                        HeadcountNeeded = shift.HeadcountNeeded,
                        MinimumHeadcount = shift.MinimumHeadcount,
                        Coverage = new ShiftCoverageDto { Status = CoverageStatus.Critical }
                    });
                }
            }

            return Task.FromResult(new BulkShiftResponse
            {
                Created = created,
                Skipped = skipped,
                Shifts = createdDtos
            });
        }

        public Task<ShiftDetailDto> GetShiftAsync(Guid id, CancellationToken ct = default)
        {
            var shift = _shifts.FirstOrDefault(s => s.Id == id);
            if (shift == null)
            {
                throw new NotFoundException("Shift", id);
            }

            return Task.FromResult(shift);
        }

        public Task<ShiftSummaryDto> UpdateShiftAsync(Guid id, CreateShiftRequest request, CancellationToken ct = default)
        {
            var shift = _shifts.FirstOrDefault(s => s.Id == id);
            if (shift == null)
            {
                throw new NotFoundException("Shift", id);
            }

            shift.WardId = request.WardId;
            shift.Date = request.Date;
            shift.StartTime = request.StartTime;
            shift.EndTime = request.EndTime;
            shift.CrossesMidnight = TimeOnly.Parse(request.EndTime) < TimeOnly.Parse(request.StartTime);
            shift.RequiredRole = request.RequiredRole;
            shift.RequiredSkillId = request.RequiredSkillId;
            shift.HeadcountNeeded = request.HeadcountNeeded;
            shift.MinimumHeadcount = request.MinimumHeadcount ?? request.HeadcountNeeded;
            shift.UpdatedAt = DateTimeOffset.UtcNow;

            return Task.FromResult(new ShiftSummaryDto
            {
                Id = shift.Id,
                WardId = shift.WardId,
                WardName = shift.WardName,
                Date = shift.Date,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                CrossesMidnight = shift.CrossesMidnight,
                RequiredRole = shift.RequiredRole,
                RequiredSkillId = shift.RequiredSkillId,
                HeadcountNeeded = shift.HeadcountNeeded,
                MinimumHeadcount = shift.MinimumHeadcount,
                Coverage = shift.Coverage,
                CreatedAt = shift.CreatedAt,
                UpdatedAt = shift.UpdatedAt
            });
        }

        public Task CancelShiftAsync(Guid id, CancellationToken ct = default)
        {
            var shift = _shifts.FirstOrDefault(s => s.Id == id);
            if (shift == null)
            {
                throw new NotFoundException("Shift", id);
            }

            var startDateTime = shift.Date.ToDateTime(TimeOnly.Parse(shift.StartTime), DateTimeKind.Utc);
            if (startDateTime <= DateTime.UtcNow)
            {
                throw new ConflictException(MessageCode.Conflict, "The shift has already started.");
            }

            foreach (var alloc in shift.Allocations)
            {
                alloc.Status = AllocationStatus.Released;
                alloc.EndedReason = AllocationEndReason.ShiftCancelled;
                alloc.EndedAt = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WardStaffingRuleDto>> GetWardStaffingRulesAsync(Guid wardId, CancellationToken ct = default)
        {
            if (_wardRules.TryGetValue(wardId, out var rules))
            {
                return Task.FromResult<IReadOnlyList<WardStaffingRuleDto>>(rules);
            }

            return Task.FromResult<IReadOnlyList<WardStaffingRuleDto>>(new List<WardStaffingRuleDto>());
        }

        public Task<ReplaceWardStaffingRulesResponse> ReplaceWardStaffingRulesAsync(
            Guid wardId,
            IReadOnlyList<WardStaffingRuleInput> rules,
            CancellationToken ct = default)
        {
            var dtos = rules.Select(r => new WardStaffingRuleDto
            {
                Id = Guid.NewGuid(),
                WardId = wardId,
                RequiredRole = r.RequiredRole,
                RequiredSkillId = r.RequiredSkillId,
                MinimumHeadcount = r.MinimumHeadcount
            }).ToList();

            _wardRules[wardId] = dtos;

            var disagreeing = _shifts
                .Where(s => s.WardId == wardId && s.Date >= DateOnly.FromDateTime(DateTime.UtcNow))
                .Where(s =>
                {
                    var rule = dtos.FirstOrDefault(r => r.RequiredRole == s.RequiredRole && r.RequiredSkillId == s.RequiredSkillId);
                    return rule != null && s.MinimumHeadcount != rule.MinimumHeadcount;
                })
                .Select(s => new ShiftSummaryDto
                {
                    Id = s.Id,
                    WardId = s.WardId,
                    WardName = s.WardName,
                    Date = s.Date,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    CrossesMidnight = s.CrossesMidnight,
                    RequiredRole = s.RequiredRole,
                    HeadcountNeeded = s.HeadcountNeeded,
                    MinimumHeadcount = s.MinimumHeadcount,
                    Coverage = s.Coverage
                })
                .ToList();

            return Task.FromResult(new ReplaceWardStaffingRulesResponse
            {
                Rules = dtos,
                ShiftsNowDisagreeing = disagreeing
            });
        }
    }

    #endregion
}
