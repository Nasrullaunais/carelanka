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

public sealed class AllocationEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/allocations", "get", "listAllocations")]
    [InlineData("/allocations", "post", "createAllocation")]
    [InlineData("/allocations/{id}/end", "post", "endAllocation")]
    public async Task Allocation_operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/allocations", "get")]
    [InlineData("/allocations", "post")]
    [InlineData("/allocations/{id}/end", "post")]
    public async Task Allocation_response_statuses_match_contract(string path, string method)
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
    public async Task Allocations_endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication();
        using var client = app.CreateClient();

        var allocationId = Guid.NewGuid();
        var getResponse = await client.GetAsync("/api/allocations");
        var postResponse = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid()
        }, JsonOptions);
        var endResponse = await client.PostAsJsonAsync($"/api/allocations/{allocationId}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, endResponse.StatusCode);
    }

    [Fact]
    public async Task Allocations_endpoints_reject_patient_token_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var allocationId = Guid.NewGuid();
        var getResponse = await client.GetAsync("/api/allocations");
        var postResponse = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid()
        }, JsonOptions);
        var endResponse = await client.PostAsJsonAsync($"/api/allocations/{allocationId}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, endResponse.StatusCode);
    }

    [Theory]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("ambulance_crew")]
    [InlineData("general_staff")]
    [InlineData("equipment_manager")]
    public async Task Allocations_endpoints_reject_non_manager_staff_roles(string role)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var allocationId = Guid.NewGuid();
        var getResponse = await client.GetAsync("/api/allocations");
        var postResponse = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid()
        }, JsonOptions);
        var endResponse = await client.PostAsJsonAsync($"/api/allocations/{allocationId}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, endResponse.StatusCode);
    }

    [Fact]
    public async Task Hospital_administrator_allowed_for_all_allocation_endpoints()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var (shift, staff, alloc) = stub.SeedValidShiftAndStaff();

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var getResponse = await client.GetAsync("/api/allocations");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var staff2Id = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var postResponse = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shift.Id,
            StaffMemberId = staff2Id
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var endResponse = await client.PostAsJsonAsync($"/api/allocations/{alloc.Id}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
    }

    [Fact]
    public async Task Duty_manager_allowed_for_all_allocation_endpoints()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var (shift, staff, alloc) = stub.SeedValidShiftAndStaff();
        var staff2Id = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var getResponse = await client.GetAsync("/api/allocations");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shift.Id,
            StaffMemberId = staff2Id
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var endResponse = await client.PostAsJsonAsync($"/api/allocations/{alloc.Id}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
    }

    #endregion

    #region Request Validation Tests

    [Fact]
    public async Task Create_allocation_rejects_empty_shift_id()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.Empty,
            StaffMemberId = Guid.NewGuid()
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_allocation_rejects_empty_staff_member_id()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.Empty
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_allocation_rejects_override_true_without_reason()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            Override = true,
            OverrideReason = null
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_allocation_rejects_override_reason_exceeding_max_length()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            Override = true,
            OverrideReason = new string('A', 501)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task End_allocation_rejects_notes_exceeding_max_length()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/allocations/{Guid.NewGuid()}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual,
            Notes = new string('N', 501)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_allocations_rejects_invalid_pagination(string query)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/allocations?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task List_allocations_rejects_to_before_from()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new AllocationTestApplication(new StubAllocationService());
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync("/api/allocations?from=2026-10-15&to=2026-10-10");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task List_allocations_returns_paged_results_with_filters()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var wardId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        stub.Allocations.Add(new AllocationDto
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StaffMemberId = staffId,
            StaffName = "Alice Silva",
            Status = AllocationStatus.Confirmed,
            Source = AllocationSource.Manual,
            CreatedAt = DateTimeOffset.UtcNow
        });
        stub.ShiftWardMap[shiftId] = wardId;
        stub.ShiftDateMap[shiftId] = new DateOnly(2026, 10, 5);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/allocations?wardId={wardId}&staffMemberId={staffId}&status=confirmed&from=2026-10-01&to=2026-10-10&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AllocationDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalItems);
        Assert.Single(result.Items);
        Assert.Equal("Alice Silva", result.Items[0].StaffName);
        Assert.Equal(AllocationStatus.Confirmed, result.Items[0].Status);
    }

    [Fact]
    public async Task Create_allocation_returns_404_when_shift_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var staffId = stub.AddStaffMember("Dr. John", StaffRole.Doctor, true);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = Guid.NewGuid(),
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_allocation_returns_404_when_staff_member_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.Doctor);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = Guid.NewGuid()
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_allocation_rejects_inactive_staff_member_with_failed_check()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.WardNurse);
        var staffId = stub.AddStaffMember("Jane Doe", StaffRole.WardNurse, isActive: false);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = doc.RootElement.GetProperty("failed_checks").EnumerateArray()
            .Select(c => c.GetProperty("check").GetString()).ToList();
        Assert.Contains("staff_active", checks);
    }

    [Fact]
    public async Task Create_allocation_rejects_role_mismatch_with_failed_check()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.Doctor);
        var staffId = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, isActive: true);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = doc.RootElement.GetProperty("failed_checks").EnumerateArray()
            .Select(c => c.GetProperty("check").GetString()).ToList();
        Assert.Contains("role_match", checks);
    }

    [Fact]
    public async Task Create_allocation_rejects_missing_or_expired_skill_with_failed_check()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var skillId = Guid.NewGuid();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.WardNurse, requiredSkillId: skillId);
        var staffId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, isActive: true);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = doc.RootElement.GetProperty("failed_checks").EnumerateArray()
            .Select(c => c.GetProperty("check").GetString()).ToList();
        Assert.Contains("required_skill", checks);
    }

    [Fact]
    public async Task Create_allocation_rejects_approved_leave_conflict_with_failed_check()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftDate = new DateOnly(2026, 10, 10);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.Doctor);
        var staffId = stub.AddStaffMember("Dr. Fernando", StaffRole.Doctor, isActive: true);

        stub.AddApprovedLeave(staffId, new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 12));

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = doc.RootElement.GetProperty("failed_checks").EnumerateArray()
            .Select(c => c.GetProperty("check").GetString()).ToList();
        Assert.Contains("leave_conflict", checks);
    }

    [Fact]
    public async Task Create_allocation_rejects_overlapping_shift_with_failed_check()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var date = new DateOnly(2026, 10, 10);
        var shiftId1 = stub.AddShift(date, "08:00", "16:00", StaffRole.WardNurse);
        var shiftId2 = stub.AddShift(date, "12:00", "20:00", StaffRole.WardNurse);
        var staffId = stub.AddStaffMember("Nurse Bandara", StaffRole.WardNurse, isActive: true);

        stub.Allocations.Add(new AllocationDto
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId1,
            StaffMemberId = staffId,
            StaffName = "Nurse Bandara",
            Status = AllocationStatus.Confirmed,
            Source = AllocationSource.Manual,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId2,
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = doc.RootElement.GetProperty("failed_checks").EnumerateArray()
            .Select(c => c.GetProperty("check").GetString()).ToList();
        Assert.Contains("shift_overlap", checks);
    }

    [Fact]
    public async Task Create_allocation_succeeds_with_override_when_rules_violated()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.Doctor);
        var staffId = stub.AddStaffMember("Nurse Bandara", StaffRole.WardNurse, isActive: true);

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId,
            Override = true,
            OverrideReason = "Special emergency acting role approved by hospital director"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AllocationDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(shiftId, created.ShiftId);
        Assert.Equal(staffId, created.StaffMemberId);
        Assert.Equal(AllocationStatus.Confirmed, created.Status);
    }

    [Fact]
    public async Task Create_allocation_rejects_duplicate_allocation_to_same_shift_even_with_override()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.WardNurse);
        var staffId = stub.AddStaffMember("Nurse Bandara", StaffRole.WardNurse, isActive: true);

        stub.Allocations.Add(new AllocationDto
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StaffMemberId = staffId,
            StaffName = "Nurse Bandara",
            Status = AllocationStatus.Confirmed,
            Source = AllocationSource.Manual,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId,
            Override = true,
            OverrideReason = "Forcing duplicate"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_allocation_succeeds_when_all_rules_pass()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var skillId = Guid.NewGuid();
        var shiftDate = new DateOnly(2026, 10, 10);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse, requiredSkillId: skillId);
        var staffId = stub.AddStaffMember("Nurse Bandara", StaffRole.WardNurse, isActive: true);
        stub.AddStaffSkill(staffId, skillId, validFrom: shiftDate.AddDays(-10), expiresAt: shiftDate.AddDays(100));

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/allocations", new CreateAllocationRequest
        {
            ShiftId = shiftId,
            StaffMemberId = staffId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AllocationDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(shiftId, created.ShiftId);
        Assert.Equal(staffId, created.StaffMemberId);
        Assert.Equal("Nurse Bandara", created.StaffName);
        Assert.Equal(AllocationStatus.Confirmed, created.Status);
        Assert.Equal(AllocationSource.Manual, created.Source);
    }

    [Fact]
    public async Task End_allocation_returns_404_when_allocation_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/allocations/{Guid.NewGuid()}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task End_allocation_returns_409_when_allocation_is_already_released()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var allocId = Guid.NewGuid();
        stub.Allocations.Add(new AllocationDto
        {
            Id = allocId,
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            Status = AllocationStatus.Released,
            Source = AllocationSource.Manual,
            EndedAt = DateTimeOffset.UtcNow,
            EndedReason = AllocationEndReason.Manual
        });

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/allocations/{allocId}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task End_allocation_returns_409_when_allocation_is_cancelled()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var allocId = Guid.NewGuid();
        stub.Allocations.Add(new AllocationDto
        {
            Id = allocId,
            ShiftId = Guid.NewGuid(),
            StaffMemberId = Guid.NewGuid(),
            Status = AllocationStatus.Cancelled,
            Source = AllocationSource.Manual
        });

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/allocations/{allocId}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task End_allocation_succeeds_and_recomputes_shift_coverage()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubAllocationService();
        var shiftId = stub.AddShift(new DateOnly(2026, 10, 10), "08:00", "16:00", StaffRole.WardNurse,
            headcountNeeded: 3, minimumHeadcount: 2);
        var staffId = stub.AddStaffMember("Nurse Bandara", StaffRole.WardNurse, isActive: true);

        var allocId = Guid.NewGuid();
        stub.Allocations.Add(new AllocationDto
        {
            Id = allocId,
            ShiftId = shiftId,
            StaffMemberId = staffId,
            StaffName = "Nurse Bandara",
            Status = AllocationStatus.Confirmed,
            Source = AllocationSource.Manual,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await using var app = new AllocationTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/allocations/{allocId}/end", new EndAllocationRequest
        {
            Reason = AllocationEndReason.Manual,
            Notes = "Re-allocating staff to Emergency"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<EndAllocationResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(AllocationStatus.Released, result.Allocation.Status);
        Assert.Equal(AllocationEndReason.Manual, result.Allocation.EndedReason);
        Assert.NotNull(result.Allocation.EndedAt);
        Assert.Null(result.RosterProposalId);

        Assert.Equal(0, result.ShiftCoverage.ConfirmedCount);
        Assert.Equal(2, result.ShiftCoverage.ShortfallToMinimum);
        Assert.Equal(CoverageStatus.Critical, result.ShiftCoverage.Status);
    }

    #endregion

    #region Helpers and Test Doubles

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
                services.AddScoped<IAllocationService, AllocationService>();
            });
        }
    }

    private sealed class AllocationTestApplication : WebApplicationFactory<Program>
    {
        private readonly IAllocationService? _stub;

        public AllocationTestApplication(IAllocationService? stub = null)
        {
            _stub = stub;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IAllocationService, AllocationService>();
                if (_stub != null)
                {
                    services.RemoveAll<IAllocationService>();
                    services.AddSingleton(_stub);
                }
            });
        }
    }

    private sealed class StubAllocationService : IAllocationService
    {
        public List<AllocationDto> Allocations { get; } = new();
        public Dictionary<Guid, Guid> ShiftWardMap { get; } = new();
        public Dictionary<Guid, DateOnly> ShiftDateMap { get; } = new();

        private readonly List<TestShift> _shifts = new();
        private readonly List<TestStaffMember> _staff = new();
        private readonly List<TestStaffSkill> _skills = new();
        private readonly List<TestLeave> _leaves = new();

        public Guid AddShift(DateOnly date, string start, string end, StaffRole role,
            Guid? requiredSkillId = null, int headcountNeeded = 2, int minimumHeadcount = 1)
        {
            var id = Guid.NewGuid();
            var wardId = Guid.NewGuid();
            _shifts.Add(new TestShift
            {
                Id = id,
                WardId = wardId,
                Date = date,
                StartTime = start,
                EndTime = end,
                RequiredRole = role,
                RequiredSkillId = requiredSkillId,
                HeadcountNeeded = headcountNeeded,
                MinimumHeadcount = minimumHeadcount
            });
            ShiftWardMap[id] = wardId;
            ShiftDateMap[id] = date;
            return id;
        }

        public Guid AddStaffMember(string name, StaffRole role, bool isActive)
        {
            var id = Guid.NewGuid();
            _staff.Add(new TestStaffMember
            {
                Id = id,
                FullName = name,
                Role = role,
                IsActive = isActive
            });
            return id;
        }

        public void AddStaffSkill(Guid staffId, Guid skillId, DateOnly? validFrom = null, DateOnly? expiresAt = null)
        {
            _skills.Add(new TestStaffSkill
            {
                StaffId = staffId,
                SkillId = skillId,
                ValidFrom = validFrom,
                ExpiresAt = expiresAt
            });
        }

        public void AddApprovedLeave(Guid staffId, DateOnly from, DateOnly to)
        {
            _leaves.Add(new TestLeave
            {
                StaffId = staffId,
                StartDate = from,
                EndDate = to
            });
        }

        public (TestShift Shift, TestStaffMember Staff, AllocationDto Allocation) SeedValidShiftAndStaff()
        {
            var shiftDate = new DateOnly(2026, 10, 10);
            var shiftId = AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
            var staffId = AddStaffMember("Sister Perera", StaffRole.WardNurse, true);
            var shift = _shifts.First(s => s.Id == shiftId);
            var staff = _staff.First(s => s.Id == staffId);

            var alloc = new AllocationDto
            {
                Id = Guid.NewGuid(),
                ShiftId = shiftId,
                StaffMemberId = staffId,
                StaffName = staff.FullName,
                Status = AllocationStatus.Confirmed,
                Source = AllocationSource.Manual,
                CreatedAt = DateTimeOffset.UtcNow
            };
            Allocations.Add(alloc);

            return (shift, staff, alloc);
        }

        public Task<PagedResult<AllocationDto>> ListAllocationsAsync(
            ListAllocationsQueryParameters parameters,
            CancellationToken cancellationToken = default)
        {
            if (parameters.From.HasValue && parameters.To.HasValue && parameters.To.Value < parameters.From.Value)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
            }

            var query = Allocations.AsEnumerable();

            if (parameters.ShiftId.HasValue)
            {
                query = query.Where(a => a.ShiftId == parameters.ShiftId.Value);
            }

            if (parameters.StaffMemberId.HasValue)
            {
                query = query.Where(a => a.StaffMemberId == parameters.StaffMemberId.Value);
            }

            if (parameters.WardId.HasValue)
            {
                query = query.Where(a => ShiftWardMap.TryGetValue(a.ShiftId, out var w) && w == parameters.WardId.Value);
            }

            if (parameters.Status.HasValue)
            {
                query = query.Where(a => a.Status == parameters.Status.Value);
            }

            if (parameters.From.HasValue)
            {
                query = query.Where(a => ShiftDateMap.TryGetValue(a.ShiftId, out var d) && d >= parameters.From.Value);
            }

            if (parameters.To.HasValue)
            {
                query = query.Where(a => ShiftDateMap.TryGetValue(a.ShiftId, out var d) && d <= parameters.To.Value);
            }

            var total = query.Count();
            var page = Math.Max(1, parameters.Page);
            var pageSize = Math.Clamp(parameters.PageSize, 1, 100);
            var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult(PagedResult<AllocationDto>.From(paged, page, pageSize, total));
        }

        public Task<AllocationDto> CreateAllocationAsync(
            CreateAllocationRequest request,
            CancellationToken cancellationToken = default)
        {
            var shift = _shifts.FirstOrDefault(s => s.Id == request.ShiftId);
            if (shift == null)
            {
                throw new NotFoundException("Shift", request.ShiftId);
            }

            var staff = _staff.FirstOrDefault(s => s.Id == request.StaffMemberId);
            if (staff == null)
            {
                throw new NotFoundException("StaffMember", request.StaffMemberId);
            }

            var failedChecks = new List<RosterValidationResult>();
            var now = DateTimeOffset.UtcNow;

            // 1. Staff active
            if (!staff.IsActive)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "staff_active",
                    Passed = false,
                    Detail = "Staff member is not active or has been deactivated.",
                    CheckedAt = now
                });
            }

            // 2. Role match
            if (staff.Role != shift.RequiredRole)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "role_match",
                    Passed = false,
                    Detail = $"Staff member role '{staff.Role}' does not match required shift role '{shift.RequiredRole}'.",
                    CheckedAt = now
                });
            }

            // 3. Required skill
            if (shift.RequiredSkillId.HasValue)
            {
                var hasSkill = _skills.Any(s => s.StaffId == staff.Id
                                                && s.SkillId == shift.RequiredSkillId.Value
                                                && (s.ValidFrom == null || s.ValidFrom.Value <= shift.Date)
                                                && (s.ExpiresAt == null || s.ExpiresAt.Value >= shift.Date));
                if (!hasSkill)
                {
                    failedChecks.Add(new RosterValidationResult
                    {
                        Check = "required_skill",
                        Passed = false,
                        Detail = "Staff member does not hold active certification on shift date.",
                        CheckedAt = now
                    });
                }
            }

            // 4. Leave conflict
            var hasLeave = _leaves.Any(l => l.StaffId == staff.Id
                                            && l.StartDate <= shift.Date
                                            && l.EndDate >= shift.Date);
            if (hasLeave)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "leave_conflict",
                    Passed = false,
                    Detail = "Staff member has approved leave.",
                    CheckedAt = now
                });
            }

            // 5. Duplicate check (same shift) & overlap
            var confirmedAllocations = Allocations.Where(a => a.StaffMemberId == staff.Id && a.Status == AllocationStatus.Confirmed).ToList();
            if (confirmedAllocations.Any(a => a.ShiftId == shift.Id))
            {
                throw new ConflictException(MessageCode.Conflict, "Staff member already has a confirmed allocation for this shift.");
            }

            var hasOverlap = confirmedAllocations.Any(a =>
            {
                var other = _shifts.FirstOrDefault(s => s.Id == a.ShiftId);
                return other != null && other.Date == shift.Date;
            });
            if (hasOverlap)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "shift_overlap",
                    Passed = false,
                    Detail = "Staff member is already allocated to an overlapping shift.",
                    CheckedAt = now
                });
            }

            if (failedChecks.Count > 0 && !request.Override)
            {
                throw new AllocationRejectedException(failedChecks, "Staff allocation was rejected due to staffing rule violations.");
            }

            var alloc = new AllocationDto
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StaffMemberId = staff.Id,
                StaffName = staff.FullName,
                Status = AllocationStatus.Confirmed,
                Source = AllocationSource.Manual,
                CreatedAt = now
            };
            Allocations.Add(alloc);

            return Task.FromResult(alloc);
        }

        public Task<EndAllocationResponse> EndAllocationAsync(
            Guid id,
            EndAllocationRequest request,
            CancellationToken cancellationToken = default)
        {
            var alloc = Allocations.FirstOrDefault(a => a.Id == id);
            if (alloc == null)
            {
                throw new NotFoundException("Allocation", id);
            }

            if (alloc.Status == AllocationStatus.Released)
            {
                throw new ConflictException(MessageCode.Conflict, "Allocation is already ended.");
            }

            if (alloc.Status == AllocationStatus.Cancelled)
            {
                throw new ConflictException(MessageCode.Conflict, "Allocation has been cancelled.");
            }

            alloc.Status = AllocationStatus.Released;
            alloc.EndedReason = request.Reason;
            alloc.EndedAt = DateTimeOffset.UtcNow;

            var shift = _shifts.FirstOrDefault(s => s.Id == alloc.ShiftId)
                        ?? new TestShift { Id = alloc.ShiftId, HeadcountNeeded = 2, MinimumHeadcount = 1 };

            var confirmedCount = Allocations.Count(a => a.ShiftId == shift.Id && a.Status == AllocationStatus.Confirmed);
            var shortfall = Math.Max(0, shift.MinimumHeadcount - confirmedCount);

            CoverageStatus status;
            if (confirmedCount >= shift.HeadcountNeeded)
            {
                status = CoverageStatus.Adequate;
            }
            else if (confirmedCount >= shift.MinimumHeadcount)
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

            var coverage = new ShiftCoverageDto
            {
                ConfirmedCount = confirmedCount,
                HeadcountNeeded = shift.HeadcountNeeded,
                MinimumHeadcount = shift.MinimumHeadcount,
                Status = status,
                ShortfallToMinimum = shortfall
            };

            return Task.FromResult(new EndAllocationResponse
            {
                Allocation = alloc,
                ShiftCoverage = coverage,
                RosterProposalId = null
            });
        }

        public class TestShift
        {
            public Guid Id { get; set; }
            public Guid WardId { get; set; }
            public DateOnly Date { get; set; }
            public string StartTime { get; set; } = "08:00";
            public string EndTime { get; set; } = "16:00";
            public StaffRole RequiredRole { get; set; }
            public Guid? RequiredSkillId { get; set; }
            public int HeadcountNeeded { get; set; } = 2;
            public int MinimumHeadcount { get; set; } = 1;
        }

        public class TestStaffMember
        {
            public Guid Id { get; set; }
            public string FullName { get; set; } = string.Empty;
            public StaffRole Role { get; set; }
            public bool IsActive { get; set; } = true;
        }

        public class TestStaffSkill
        {
            public Guid StaffId { get; set; }
            public Guid SkillId { get; set; }
            public DateOnly? ValidFrom { get; set; }
            public DateOnly? ExpiresAt { get; set; }
        }

        public class TestLeave
        {
            public Guid StaffId { get; set; }
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
        }
    }

    #endregion
}
