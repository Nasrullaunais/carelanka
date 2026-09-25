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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests;

public sealed class LeaveRequestEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/leave-requests", "get", "listLeaveRequests")]
    [InlineData("/leave-requests/{id}", "get", "getLeaveRequest")]
    [InlineData("/leave-requests/{id}/decision", "post", "decideLeaveRequest")]
    [InlineData("/me/leave-requests", "get", "getMyLeaveRequests")]
    [InlineData("/me/leave-requests", "post", "createMyLeaveRequest")]
    [InlineData("/me/leave-requests/{id}", "delete", "withdrawMyLeaveRequest")]
    public async Task Leave_request_operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/leave-requests", "get")]
    [InlineData("/leave-requests/{id}", "get")]
    [InlineData("/leave-requests/{id}/decision", "post")]
    [InlineData("/me/leave-requests", "get")]
    [InlineData("/me/leave-requests", "post")]
    [InlineData("/me/leave-requests/{id}", "delete")]
    public async Task Leave_request_response_statuses_match_contract(string path, string method)
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
    public async Task Leave_requests_endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();

        var leaveId = Guid.NewGuid();

        var getQueueResponse = await client.GetAsync("/api/leave-requests");
        var getDetailResponse = await client.GetAsync($"/api/leave-requests/{leaveId}");
        var postDecisionResponse = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve"
        }, JsonOptions);
        var getMyResponse = await client.GetAsync("/api/me/leave-requests");
        var postMyResponse = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 5)
        }, JsonOptions);
        var deleteMyResponse = await client.DeleteAsync($"/api/me/leave-requests/{leaveId}");

        Assert.Equal(HttpStatusCode.Unauthorized, getQueueResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getDetailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postDecisionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getMyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postMyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteMyResponse.StatusCode);
    }

    [Fact]
    public async Task Leave_requests_endpoints_reject_patient_token_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var leaveId = Guid.NewGuid();

        var getQueueResponse = await client.GetAsync("/api/leave-requests");
        var getDetailResponse = await client.GetAsync($"/api/leave-requests/{leaveId}");
        var postDecisionResponse = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve"
        }, JsonOptions);
        var getMyResponse = await client.GetAsync("/api/me/leave-requests");
        var postMyResponse = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 5)
        }, JsonOptions);
        var deleteMyResponse = await client.DeleteAsync($"/api/me/leave-requests/{leaveId}");

        Assert.Equal(HttpStatusCode.Forbidden, getQueueResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getDetailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postDecisionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getMyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postMyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteMyResponse.StatusCode);
    }

    [Theory]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("ambulance_crew")]
    [InlineData("general_staff")]
    [InlineData("equipment_manager")]
    public async Task Queue_and_decision_endpoints_reject_non_manager_staff_roles(string role)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var leaveId = Guid.NewGuid();

        var queueResponse = await client.GetAsync("/api/leave-requests");
        var decisionResponse = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, queueResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, decisionResponse.StatusCode);
    }

    [Theory]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("ambulance_crew")]
    [InlineData("general_staff")]
    [InlineData("equipment_manager")]
    public async Task Staff_roles_allowed_for_my_leave_requests_endpoints(string role)
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Staff Member", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(staffId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff", staffId));

        var getResponse = await client.GetAsync("/api/me/leave-requests");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Sick,
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 12),
            Reason = "Flu symptoms"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/me/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Requester_can_view_own_leave_request_detail()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(staffId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", staffId));

        var response = await client.GetAsync($"/api/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(leaveId, detail.Id);
        Assert.Equal(staffId, detail.StaffMemberId);
    }

    [Fact]
    public async Task Staff_member_cannot_view_another_staff_members_leave_request_detail()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staff1Id = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, true);
        var staff2Id = stub.AddStaffMember("Dr. Silva", StaffRole.Doctor, true);
        var leaveId = stub.AddLeaveRequest(staff1Id, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("doctor", "staff", staff2Id));

        var response = await client.GetAsync($"/api/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("hospital_administrator")]
    [InlineData("duty_manager")]
    public async Task Manager_roles_can_view_any_leave_request_detail(string role)
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(staffId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var response = await client.GetAsync($"/api/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reviewer_cannot_decide_own_leave_request_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var managerId = stub.AddStaffMember("Duty Manager", StaffRole.DutyManager, true);
        var leaveId = stub.AddLeaveRequest(managerId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff", managerId));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_403", body);
    }

    [Theory]
    [InlineData("hospital_administrator")]
    [InlineData("duty_manager")]
    public async Task Hospital_administrator_and_duty_manager_allowed_for_queue_and_decisions(string role)
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(staffId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var queueResponse = await client.GetAsync("/api/leave-requests");
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);

        var decisionResponse = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve",
            Notes = "Approved by manager"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
    }

    #endregion

    #region Request Validation Tests

    [Fact]
    public async Task Create_leave_request_rejects_missing_dates_for_date_based_leave()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = null,
            EndDate = null
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_leave_request_rejects_start_date_after_end_date()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = new DateOnly(2026, 11, 10),
            EndDate = new DateOnly(2026, 11, 5)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_leave_request_rejects_reason_exceeding_max_length()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 5),
            Reason = new string('A', 501)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_missing_swap_shift_id()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = null,
            SwapWithStaffMemberId = Guid.NewGuid()
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_missing_swap_with_staff_member_id()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = Guid.NewGuid(),
            SwapWithStaffMemberId = null
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("")]
    [InlineData("pending")]
    public async Task Decide_leave_request_rejects_invalid_decision_string(string decision)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{Guid.NewGuid()}/decision", new DecideLeaveRequest
        {
            Decision = decision
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task Decide_leave_request_rejects_notes_exceeding_max_length()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{Guid.NewGuid()}/decision", new DecideLeaveRequest
        {
            Decision = "approve",
            Notes = new string('N', 501)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Fact]
    public async Task List_leave_requests_rejects_to_before_from()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync("/api/leave-requests?from=2026-11-15&to=2026-11-10");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_leave_requests_rejects_invalid_pagination(string query)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/leave-requests?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_400", body);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task Create_date_based_leave_request_success()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", staffId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = new DateOnly(2026, 12, 1),
            EndDate = new DateOnly(2026, 12, 10),
            Reason = "Family vacation"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(staffId, detail.StaffMemberId);
        Assert.Equal(LeaveType.Annual, detail.Type);
        Assert.Equal(LeaveStatus.Pending, detail.Status);
        Assert.False(detail.IsUrgent);
        Assert.Equal("Family vacation", detail.Reason);
    }

    [Theory]
    [InlineData(LeaveType.Sick)]
    [InlineData(LeaveType.Emergency)]
    public async Task Create_urgent_leave_request_sets_is_urgent_true_for_sick_and_emergency(LeaveType type)
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", staffId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = type,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
            Reason = "Urgent situation"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detail);
        Assert.True(detail.IsUrgent);
    }

    [Fact]
    public async Task Create_annual_leave_urgency_based_on_start_date()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", staffId));

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var responseUrgent = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = tomorrow,
            EndDate = tomorrow.AddDays(1),
            Reason = "Last minute request"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, responseUrgent.StatusCode);
        var detailUrgent = await responseUrgent.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detailUrgent);
        Assert.True(detailUrgent.IsUrgent);

        var staff2Id = stub.AddStaffMember("Dr. John", StaffRole.Doctor, true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("doctor", "staff", staff2Id));

        var nextMonth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var responseNotUrgent = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = nextMonth,
            EndDate = nextMonth.AddDays(3),
            Reason = "Planned vacation"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, responseNotUrgent.StatusCode);
        var detailNotUrgent = await responseNotUrgent.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detailNotUrgent);
        Assert.False(detailNotUrgent.IsUrgent);
    }

    [Fact]
    public async Task Create_leave_request_rejects_inactive_requester_with_not_found()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Inactive Nurse", StaffRole.WardNurse, isActive: false);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", staffId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Annual,
            StartDate = new DateOnly(2026, 12, 1),
            EndDate = new DateOnly(2026, 12, 5)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_leave_request_rejects_overlapping_existing_leave_with_conflict()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var staffId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        stub.AddLeaveRequest(staffId, LeaveType.Annual, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 15), LeaveStatus.Approved);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", staffId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.Sick,
            StartDate = new DateOnly(2026, 11, 12),
            EndDate = new DateOnly(2026, 11, 18)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_409", body);
    }

    [Fact]
    public async Task Create_shift_swap_success()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 20);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        stub.AddAllocation(shiftId, nurseAId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = nurseBId,
            Reason = "Can colleague cover my shift?"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(LeaveType.ShiftSwap, detail.Type);
        Assert.Equal(shiftId, detail.SwapShiftId);
        Assert.Equal(nurseBId, detail.SwapWithStaffMemberId);
        Assert.Single(detail.AffectedShifts);
        Assert.Equal(shiftId, detail.AffectedShifts[0].Shift.Id);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_swapping_with_oneself()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var shiftId = stub.AddShift(new DateOnly(2026, 11, 20), "08:00", "16:00", StaffRole.WardNurse);
        stub.AddAllocation(shiftId, nurseId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = nurseId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_409", body);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_when_requester_not_allocated_to_shift()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var shiftId = stub.AddShift(new DateOnly(2026, 11, 20), "08:00", "16:00", StaffRole.WardNurse);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = nurseBId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_when_partner_not_found_or_inactive()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var inactivePartnerId = stub.AddStaffMember("Nurse Inactive", StaffRole.WardNurse, isActive: false);
        var shiftId = stub.AddShift(new DateOnly(2026, 11, 20), "08:00", "16:00", StaffRole.WardNurse);
        stub.AddAllocation(shiftId, nurseAId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = inactivePartnerId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_when_partner_role_mismatch()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var doctorId = stub.AddStaffMember("Dr. John", StaffRole.Doctor, true);
        var shiftId = stub.AddShift(new DateOnly(2026, 11, 20), "08:00", "16:00", StaffRole.WardNurse);
        stub.AddAllocation(shiftId, nurseAId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = doctorId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_409", body);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_when_partner_missing_required_skill()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var skillIcu = Guid.NewGuid();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 20);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse, requiredSkillId: skillIcu);
        stub.AddAllocation(shiftId, nurseAId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = nurseBId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_when_partner_has_overlapping_shift()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 20);
        var shift1Id = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        var shift2Id = stub.AddShift(shiftDate, "12:00", "20:00", StaffRole.WardNurse);
        stub.AddAllocation(shift1Id, nurseAId);
        stub.AddAllocation(shift2Id, nurseBId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shift1Id,
            SwapWithStaffMemberId = nurseBId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_409", body);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_when_partner_on_leave()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 20);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        stub.AddAllocation(shiftId, nurseAId);
        stub.AddLeaveRequest(nurseBId, LeaveType.Annual, shiftDate.AddDays(-2), shiftDate.AddDays(2), LeaveStatus.Approved);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = nurseBId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_shift_swap_rejects_duplicate_swap_request_for_same_shift()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var nurseCId = stub.AddStaffMember("Nurse C", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 20);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        stub.AddAllocation(shiftId, nurseAId);
        stub.AddLeaveRequest(nurseAId, LeaveType.ShiftSwap, shiftDate, shiftDate, LeaveStatus.Pending, swapShiftId: shiftId, swapWithStaffMemberId: nurseBId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.PostAsJsonAsync("/api/me/leave-requests", new CreateLeaveRequest
        {
            Type = LeaveType.ShiftSwap,
            SwapShiftId = shiftId,
            SwapWithStaffMemberId = nurseCId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task List_leave_requests_filters_by_staff_status_type_and_dates()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var otherId = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, true);

        var leave1Id = stub.AddLeaveRequest(nurseId, LeaveType.Annual, new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 8), LeaveStatus.Pending);
        stub.AddLeaveRequest(otherId, LeaveType.Sick, new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 8), LeaveStatus.Pending);
        stub.AddLeaveRequest(nurseId, LeaveType.Sick, new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 18), LeaveStatus.Approved);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/leave-requests?staffMemberId={nurseId}&status=pending&type=annual&from=2026-11-01&to=2026-11-10&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<LeaveRequestDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalItems);
        Assert.Single(result.Items);
        Assert.Equal(leave1Id, result.Items[0].Id);
    }

    [Fact]
    public async Task List_leave_requests_orders_pending_first_then_earliest_start_date()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);

        var approvedSoon = stub.AddLeaveRequest(nurseId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), LeaveStatus.Approved);
        var pendingLater = stub.AddLeaveRequest(nurseId, LeaveType.Annual, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 25), LeaveStatus.Pending);
        var pendingSooner = stub.AddLeaveRequest(nurseId, LeaveType.Sick, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), LeaveStatus.Pending);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync("/api/leave-requests?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<LeaveRequestDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalItems);

        Assert.Equal(pendingSooner, result.Items[0].Id);
        Assert.Equal(pendingLater, result.Items[1].Id);
        Assert.Equal(approvedSoon, result.Items[2].Id);
    }

    [Fact]
    public async Task Get_leave_request_detail_returns_affected_shifts_and_coverage()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var otherNurseId = stub.AddStaffMember("Nurse Perera", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 15);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse, headcountNeeded: 2, minimumHeadcount: 1);
        stub.AddAllocation(shiftId, nurseId);
        stub.AddAllocation(shiftId, otherNurseId);

        var leaveId = stub.AddLeaveRequest(nurseId, LeaveType.Annual, shiftDate, shiftDate);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<LeaveRequestDetailDto>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(leaveId, detail.Id);
        Assert.Single(detail.AffectedShifts);
        var affected = detail.AffectedShifts[0];
        Assert.Equal(shiftId, affected.Shift.Id);
        Assert.Equal(1, affected.CoverageIfApproved.ConfirmedCount);
        Assert.Equal(CoverageStatus.AtMinimum, affected.CoverageIfApproved.Status);
    }

    [Fact]
    public async Task Get_leave_request_detail_returns_404_when_not_found()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.GetAsync($"/api/leave-requests/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Decide_leave_request_approve_releases_allocations_with_reason_leave_approved()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 15);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        var allocId = stub.AddAllocation(shiftId, nurseId);

        var leaveId = stub.AddLeaveRequest(nurseId, LeaveType.Annual, shiftDate, shiftDate);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve",
            Notes = "Approved by duty manager"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DecideLeaveResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Approved, result.LeaveRequest.Status);
        Assert.Equal("Approved by duty manager", result.LeaveRequest.ReviewNotes);
        Assert.Single(result.ReleasedAllocations);
        Assert.Equal(allocId, result.ReleasedAllocations[0].AllocationId);
        Assert.Equal(AllocationStatus.Released, result.ReleasedAllocations[0].Status);

        var releasedAlloc = stub.Allocations.First(a => a.Id == allocId);
        Assert.Equal(AllocationEndReason.LeaveApproved, releasedAlloc.EndedReason);
    }

    [Fact]
    public async Task Decide_leave_request_approve_shift_swap_releases_and_creates_replacement()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 20);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        var allocId = stub.AddAllocation(shiftId, nurseAId);

        var leaveId = stub.AddLeaveRequest(nurseAId, LeaveType.ShiftSwap, shiftDate, shiftDate,
            swapShiftId: shiftId, swapWithStaffMemberId: nurseBId);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "approve",
            Notes = "Swap approved"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DecideLeaveResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Approved, result.LeaveRequest.Status);
        Assert.Single(result.ReleasedAllocations);
        Assert.Equal(allocId, result.ReleasedAllocations[0].AllocationId);
        Assert.Equal(AllocationStatus.Released, result.ReleasedAllocations[0].Status);

        var outgoingAlloc = stub.Allocations.First(a => a.Id == allocId);
        Assert.Equal(AllocationEndReason.SwappedOut, outgoingAlloc.EndedReason);
        Assert.NotNull(outgoingAlloc.ReplacedByAllocationId);

        var replacementAlloc = stub.Allocations.FirstOrDefault(a => a.ShiftId == shiftId && a.StaffMemberId == nurseBId);
        Assert.NotNull(replacementAlloc);
        Assert.Equal(AllocationStatus.Confirmed, replacementAlloc.Status);
        Assert.Equal(AllocationSource.SwapRequest, replacementAlloc.Source);
        Assert.Equal(replacementAlloc.Id, outgoingAlloc.ReplacedByAllocationId);
    }

    [Fact]
    public async Task Decide_leave_request_reject_updates_status_without_releasing_allocations()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var shiftDate = new DateOnly(2026, 11, 15);
        var shiftId = stub.AddShift(shiftDate, "08:00", "16:00", StaffRole.WardNurse);
        var allocId = stub.AddAllocation(shiftId, nurseId);

        var leaveId = stub.AddLeaveRequest(nurseId, LeaveType.Annual, shiftDate, shiftDate);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "reject",
            Notes = "Staffing too low"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DecideLeaveResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Rejected, result.LeaveRequest.Status);
        Assert.Equal("Staffing too low", result.LeaveRequest.ReviewNotes);
        Assert.Empty(result.ReleasedAllocations);

        var alloc = stub.Allocations.First(a => a.Id == allocId);
        Assert.Equal(AllocationStatus.Confirmed, alloc.Status);
    }

    [Fact]
    public async Task Decide_leave_request_returns_404_when_not_found()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{Guid.NewGuid()}/decision", new DecideLeaveRequest
        {
            Decision = "approve"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Decide_leave_request_returns_409_for_already_decided_request()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(nurseId, LeaveType.Annual, new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 18), LeaveStatus.Approved);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        var response = await client.PostAsJsonAsync($"/api/leave-requests/{leaveId}/decision", new DecideLeaveRequest
        {
            Decision = "reject"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_409", body);
    }

    [Fact]
    public async Task Get_my_leave_requests_returns_only_callers_requests()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);

        var leaveA1 = stub.AddLeaveRequest(nurseAId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5));
        var leaveA2 = stub.AddLeaveRequest(nurseAId, LeaveType.Sick, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12));
        stub.AddLeaveRequest(nurseBId, LeaveType.Annual, new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 20));

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.GetAsync("/api/me/leave-requests");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<LeaveRequestDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, l => l.Id == leaveA1);
        Assert.Contains(list, l => l.Id == leaveA2);
        Assert.DoesNotContain(list, l => l.StaffMemberId == nurseBId);
    }

    [Fact]
    public async Task Get_my_leave_requests_filters_by_status()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);

        var pendingLeave = stub.AddLeaveRequest(nurseAId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), LeaveStatus.Pending);
        stub.AddLeaveRequest(nurseAId, LeaveType.Sick, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), LeaveStatus.Approved);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseAId));

        var response = await client.GetAsync("/api/me/leave-requests?status=pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<LeaveRequestDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(pendingLeave, list[0].Id);
        Assert.Equal(LeaveStatus.Pending, list[0].Status);
    }

    [Fact]
    public async Task Withdraw_my_leave_request_success()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(nurseId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), LeaveStatus.Pending);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseId));

        var response = await client.DeleteAsync($"/api/me/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var leave = stub.LeaveRequests.First(l => l.Id == leaveId);
        Assert.Equal(LeaveStatus.Withdrawn, leave.Status);
    }

    [Fact]
    public async Task Withdraw_my_leave_request_returns_403_when_caller_is_not_requester()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseAId = stub.AddStaffMember("Nurse A", StaffRole.WardNurse, true);
        var nurseBId = stub.AddStaffMember("Nurse B", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(nurseAId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), LeaveStatus.Pending);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseBId));

        var response = await client.DeleteAsync($"/api/me/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_403", body);
    }

    [Fact]
    public async Task Withdraw_my_leave_request_returns_409_when_request_is_not_pending()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubLeaveRequestService();
        var nurseId = stub.AddStaffMember("Nurse Silva", StaffRole.WardNurse, true);
        var leaveId = stub.AddLeaveRequest(nurseId, LeaveType.Annual, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 5), LeaveStatus.Approved);

        await using var app = new LeaveRequestTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff", nurseId));

        var response = await client.DeleteAsync($"/api/me/leave-requests/{leaveId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cl_err_409", body);
    }

    [Fact]
    public async Task Withdraw_my_leave_request_returns_404_when_not_found()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new LeaveRequestTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("ward_nurse", "staff"));

        var response = await client.DeleteAsync($"/api/me/leave-requests/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private sealed class LeaveRequestTestApplication : WebApplicationFactory<Program>
    {
        private readonly StubLeaveRequestService _stub;

        public LeaveRequestTestApplication(StubLeaveRequestService? stub = null)
        {
            _stub = stub ?? new StubLeaveRequestService();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILeaveRequestService>();
                services.AddScoped<ILeaveRequestService>(sp =>
                {
                    _stub.Accessor = sp.GetRequiredService<IHttpContextAccessor>();
                    return _stub;
                });
            });
        }
    }

    public sealed class StubLeaveRequestService : ILeaveRequestService
    {
        public List<TestStaffMember> Staff { get; } = new();
        public List<TestShift> Shifts { get; } = new();
        public List<TestAllocation> Allocations { get; } = new();
        public List<TestStaffSkill> Skills { get; } = new();
        public List<TestLeaveRequest> LeaveRequests { get; } = new();
        public Dictionary<Guid, string> WardNames { get; } = new();

        public IHttpContextAccessor? Accessor { get; set; }

        private Guid CallerId
        {
            get
            {
                var sub = Accessor?.HttpContext?.User.FindFirst(CareLankaClaims.Subject)?.Value;
                return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
            }
        }

        private string? CallerRole => Accessor?.HttpContext?.User.FindFirst(CareLankaClaims.Role)?.Value;

        public Guid AddStaffMember(string name, StaffRole role, bool isActive = true, Guid? id = null)
        {
            var staffId = id ?? Guid.NewGuid();
            Staff.Add(new TestStaffMember
            {
                Id = staffId,
                FullName = name,
                Role = role,
                IsActive = isActive
            });
            return staffId;
        }

        public Guid AddShift(DateOnly date, string start, string end, StaffRole role,
            Guid? requiredSkillId = null, int headcountNeeded = 2, int minimumHeadcount = 1, Guid? wardId = null)
        {
            var id = Guid.NewGuid();
            var ward = wardId ?? Guid.NewGuid();
            if (!WardNames.ContainsKey(ward))
            {
                WardNames[ward] = "Ward A";
            }
            Shifts.Add(new TestShift
            {
                Id = id,
                WardId = ward,
                Date = date,
                StartTime = start,
                EndTime = end,
                RequiredRole = role,
                RequiredSkillId = requiredSkillId,
                HeadcountNeeded = headcountNeeded,
                MinimumHeadcount = minimumHeadcount
            });
            return id;
        }

        public Guid AddAllocation(Guid shiftId, Guid staffMemberId, AllocationStatus status = AllocationStatus.Confirmed)
        {
            var id = Guid.NewGuid();
            Allocations.Add(new TestAllocation
            {
                Id = id,
                ShiftId = shiftId,
                StaffMemberId = staffMemberId,
                Status = status
            });
            return id;
        }

        public void AddSkill(Guid staffId, Guid skillId, DateOnly? validFrom = null, DateOnly? expiresAt = null)
        {
            Skills.Add(new TestStaffSkill
            {
                StaffId = staffId,
                SkillId = skillId,
                ValidFrom = validFrom,
                ExpiresAt = expiresAt
            });
        }

        public Guid AddLeaveRequest(Guid staffMemberId, LeaveType type, DateOnly start, DateOnly end,
            LeaveStatus status = LeaveStatus.Pending, string? reason = null,
            Guid? swapShiftId = null, Guid? swapWithStaffMemberId = null)
        {
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            LeaveRequests.Add(new TestLeaveRequest
            {
                Id = id,
                StaffMemberId = staffMemberId,
                Type = type,
                StartDate = start,
                EndDate = end,
                Reason = reason,
                Status = status,
                SwapShiftId = swapShiftId,
                SwapWithStaffMemberId = swapWithStaffMemberId,
                CreatedAt = now,
                UpdatedAt = now
            });
            return id;
        }

        public Task<LeaveRequestDetailDto> CreateLeaveRequestAsync(
            CreateLeaveRequest request,
            CancellationToken cancellationToken = default)
        {
            var requesterId = CallerId;
            var requester = Staff.FirstOrDefault(s => s.Id == requesterId && s.IsActive);
            if (requester == null)
            {
                throw new NotFoundException("StaffMember", requesterId);
            }

            DateOnly startDate;
            DateOnly endDate;

            if (request.Type == LeaveType.ShiftSwap)
            {
                if (!request.SwapShiftId.HasValue || !request.SwapWithStaffMemberId.HasValue)
                {
                    throw new BadRequestException(MessageCode.ValidationFailed, "Swap shift ID and swap colleague ID are required for a shift swap.");
                }

                if (request.SwapWithStaffMemberId.Value == requesterId)
                {
                    throw new ConflictException(MessageCode.Conflict, "Cannot swap shift with yourself.");
                }

                var shift = Shifts.FirstOrDefault(s => s.Id == request.SwapShiftId.Value);
                if (shift == null)
                {
                    throw new NotFoundException("Shift", request.SwapShiftId.Value);
                }

                var requesterAllocated = Allocations.Any(a =>
                    a.ShiftId == shift.Id && a.StaffMemberId == requesterId && a.Status == AllocationStatus.Confirmed);

                if (!requesterAllocated)
                {
                    throw new ConflictException(MessageCode.Conflict, "Staff member is not allocated to the specified shift.");
                }

                var partnerId = request.SwapWithStaffMemberId.Value;
                var partner = Staff.FirstOrDefault(s => s.Id == partnerId && s.IsActive);
                if (partner == null)
                {
                    throw new NotFoundException("StaffMember", partnerId);
                }

                if (partner.Role != shift.RequiredRole)
                {
                    throw new ConflictException(MessageCode.Conflict, "Swap partner does not hold the required role for this shift.");
                }

                if (shift.RequiredSkillId.HasValue)
                {
                    var hasSkill = Skills.Any(s =>
                        s.StaffId == partnerId
                        && s.SkillId == shift.RequiredSkillId.Value
                        && (!s.ValidFrom.HasValue || s.ValidFrom.Value <= shift.Date)
                        && (!s.ExpiresAt.HasValue || s.ExpiresAt.Value >= shift.Date));

                    if (!hasSkill)
                    {
                        throw new ConflictException(MessageCode.Conflict, "Swap partner does not hold the required skill for this shift.");
                    }
                }

                var (shiftStart, shiftEnd) = GetShiftDateTimeRange(shift);
                var partnerAllocs = Allocations.Where(a => a.StaffMemberId == partnerId && a.Status == AllocationStatus.Confirmed).ToList();
                foreach (var alloc in partnerAllocs)
                {
                    var otherShift = Shifts.FirstOrDefault(s => s.Id == alloc.ShiftId);
                    if (otherShift != null)
                    {
                        var (allocStart, allocEnd) = GetShiftDateTimeRange(otherShift);
                        if (shiftStart < allocEnd && shiftEnd > allocStart)
                        {
                            throw new ConflictException(MessageCode.Conflict, "Swap partner is not free during the shift hours.");
                        }
                    }
                }

                var partnerOnLeave = LeaveRequests.Any(l =>
                    l.StaffMemberId == partnerId
                    && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
                    && l.StartDate <= shift.Date
                    && l.EndDate >= shift.Date);

                if (partnerOnLeave)
                {
                    throw new ConflictException(MessageCode.Conflict, "Swap partner has an existing leave or swap request covering this shift date.");
                }

                var existingSwap = LeaveRequests.Any(l =>
                    l.StaffMemberId == requesterId
                    && l.SwapShiftId == shift.Id
                    && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved));

                if (existingSwap)
                {
                    throw new ConflictException(MessageCode.Conflict, "A leave or swap request already exists for this shift.");
                }

                startDate = shift.Date;
                endDate = shift.Date;
            }
            else
            {
                if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                {
                    throw new BadRequestException(MessageCode.ValidationFailed, "Start date and end date are required.");
                }

                startDate = request.StartDate.Value;
                endDate = request.EndDate.Value;

                if (startDate > endDate)
                {
                    throw new BadRequestException(MessageCode.ValidationFailed, "Start date must not be after end date.");
                }

                var overlappingLeave = LeaveRequests.Any(l =>
                    l.StaffMemberId == requesterId
                    && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
                    && l.StartDate <= endDate
                    && l.EndDate >= startDate);

                if (overlappingLeave)
                {
                    throw new ConflictException(MessageCode.Conflict, "A pending or approved leave request already exists covering this date range.");
                }
            }

            var now = DateTimeOffset.UtcNow;
            var leave = new TestLeaveRequest
            {
                Id = Guid.NewGuid(),
                StaffMemberId = requesterId,
                Type = request.Type,
                StartDate = startDate,
                EndDate = endDate,
                Reason = request.Reason,
                Status = LeaveStatus.Pending,
                SwapShiftId = request.SwapShiftId,
                SwapWithStaffMemberId = request.SwapWithStaffMemberId,
                CreatedAt = now,
                UpdatedAt = now
            };
            LeaveRequests.Add(leave);

            return GetLeaveRequestDetailAsync(leave.Id, cancellationToken);
        }

        public Task<PagedResult<LeaveRequestDto>> ListLeaveRequestsAsync(
            ListLeaveRequestsQueryParameters parameters,
            CancellationToken cancellationToken = default)
        {
            if (CallerRole != "hospital_administrator" && CallerRole != "duty_manager")
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            if (parameters.From.HasValue && parameters.To.HasValue && parameters.To.Value < parameters.From.Value)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
            }

            var query = LeaveRequests.AsEnumerable();

            if (parameters.StaffMemberId.HasValue)
            {
                query = query.Where(l => l.StaffMemberId == parameters.StaffMemberId.Value);
            }

            if (parameters.Status.HasValue)
            {
                query = query.Where(l => l.Status == parameters.Status.Value);
            }

            if (parameters.Type.HasValue)
            {
                query = query.Where(l => l.Type == parameters.Type.Value);
            }

            if (parameters.From.HasValue)
            {
                query = query.Where(l => l.StartDate >= parameters.From.Value);
            }

            if (parameters.To.HasValue)
            {
                query = query.Where(l => l.StartDate <= parameters.To.Value);
            }

            var totalItems = query.Count();

            var items = query
                .OrderBy(l => l.Status == LeaveStatus.Pending ? 0 : 1)
                .ThenBy(l => l.StartDate)
                .ThenBy(l => l.CreatedAt)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToList();

            var dtos = items.Select(l =>
            {
                var staff = Staff.FirstOrDefault(s => s.Id == l.StaffMemberId);
                return MapToDto(l, staff?.FullName ?? string.Empty);
            }).ToList();

            return Task.FromResult(PagedResult<LeaveRequestDto>.From(dtos, parameters.Page, parameters.PageSize, totalItems));
        }

        public Task<LeaveRequestDetailDto> GetLeaveRequestDetailAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var leave = LeaveRequests.FirstOrDefault(l => l.Id == id);
            if (leave == null)
            {
                throw new NotFoundException("LeaveRequest", id);
            }

            if (CallerRole != "hospital_administrator" && CallerRole != "duty_manager" && CallerId != leave.StaffMemberId)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            var staff = Staff.FirstOrDefault(s => s.Id == leave.StaffMemberId);
            var affectedShifts = new List<AffectedShiftDto>();

            if (leave.Type == LeaveType.ShiftSwap && leave.SwapShiftId.HasValue)
            {
                var shift = Shifts.FirstOrDefault(s => s.Id == leave.SwapShiftId.Value);
                if (shift != null)
                {
                    var confirmedCount = Allocations.Count(a => a.ShiftId == shift.Id && a.Status == AllocationStatus.Confirmed);
                    affectedShifts.Add(new AffectedShiftDto
                    {
                        Shift = MapToShiftSummaryDto(shift, confirmedCount),
                        CoverageIfApproved = CalculateCoverage(shift, confirmedCount)
                    });
                }
            }
            else
            {
                var allocs = Allocations.Where(a => a.StaffMemberId == leave.StaffMemberId && a.Status == AllocationStatus.Confirmed).ToList();
                foreach (var alloc in allocs)
                {
                    var shift = Shifts.FirstOrDefault(s => s.Id == alloc.ShiftId);
                    if (shift != null && shift.Date >= leave.StartDate && shift.Date <= leave.EndDate)
                    {
                        var currentCount = Allocations.Count(a => a.ShiftId == shift.Id && a.Status == AllocationStatus.Confirmed);
                        var newCount = Math.Max(0, currentCount - 1);
                        affectedShifts.Add(new AffectedShiftDto
                        {
                            Shift = MapToShiftSummaryDto(shift, currentCount),
                            CoverageIfApproved = CalculateCoverage(shift, newCount)
                        });
                    }
                }
            }

            var detail = new LeaveRequestDetailDto
            {
                Id = leave.Id,
                StaffMemberId = leave.StaffMemberId,
                StaffName = staff?.FullName ?? string.Empty,
                Type = leave.Type,
                IsUrgent = ComputeIsUrgent(leave.Type, leave.StartDate),
                StartDate = leave.StartDate,
                EndDate = leave.EndDate,
                Reason = leave.Reason,
                Status = leave.Status,
                ReviewedByStaffId = leave.ReviewedByStaffMemberId,
                ReviewedAt = leave.ReviewedByStaffMemberId.HasValue ? leave.UpdatedAt : null,
                ReviewNotes = leave.ReviewNotes,
                SwapWithStaffMemberId = leave.SwapWithStaffMemberId,
                SwapShiftId = leave.SwapShiftId,
                CreatedAt = leave.CreatedAt,
                AffectedShifts = affectedShifts
            };

            return Task.FromResult(detail);
        }

        public Task<DecideLeaveResponse> DecideLeaveRequestAsync(
            Guid id,
            DecideLeaveRequest request,
            CancellationToken cancellationToken = default)
        {
            if (CallerRole != "hospital_administrator" && CallerRole != "duty_manager")
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            var leave = LeaveRequests.FirstOrDefault(l => l.Id == id);
            if (leave == null)
            {
                throw new NotFoundException("LeaveRequest", id);
            }

            if (leave.StaffMemberId == CallerId)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            var isApprove = string.Equals(request.Decision?.Trim(), "approve", StringComparison.OrdinalIgnoreCase);
            var isReject = string.Equals(request.Decision?.Trim(), "reject", StringComparison.OrdinalIgnoreCase);

            if (!isApprove && !isReject)
            {
                throw new BadRequestException(MessageCode.ValidationFailed, "Decision must be either 'approve' or 'reject'.");
            }

            if (leave.Status != LeaveStatus.Pending)
            {
                var target = isApprove ? LeaveStatus.Approved : LeaveStatus.Rejected;
                throw new IllegalTransitionException("LeaveRequest", leave.Status.ToString(), target.ToString());
            }

            var now = DateTimeOffset.UtcNow;
            leave.ReviewedByStaffMemberId = CallerId;
            leave.ReviewNotes = request.Notes;
            leave.UpdatedAt = now;

            var releasedSummaries = new List<AllocationSummaryDto>();

            if (isReject)
            {
                leave.Status = LeaveStatus.Rejected;
            }
            else
            {
                leave.Status = LeaveStatus.Approved;

                if (leave.Type == LeaveType.ShiftSwap && leave.SwapShiftId.HasValue && leave.SwapWithStaffMemberId.HasValue)
                {
                    var outgoing = Allocations.FirstOrDefault(a =>
                        a.ShiftId == leave.SwapShiftId.Value
                        && a.StaffMemberId == leave.StaffMemberId
                        && a.Status == AllocationStatus.Confirmed);

                    if (outgoing != null)
                    {
                        var incoming = new TestAllocation
                        {
                            Id = Guid.NewGuid(),
                            ShiftId = leave.SwapShiftId.Value,
                            StaffMemberId = leave.SwapWithStaffMemberId.Value,
                            Status = AllocationStatus.Confirmed,
                            Source = AllocationSource.SwapRequest
                        };
                        Allocations.Add(incoming);

                        outgoing.Status = AllocationStatus.Released;
                        outgoing.EndedReason = AllocationEndReason.SwappedOut;
                        outgoing.EndedAt = now;
                        outgoing.ReplacedByAllocationId = incoming.Id;

                        var shift = Shifts.FirstOrDefault(s => s.Id == outgoing.ShiftId);
                        var requester = Staff.FirstOrDefault(s => s.Id == leave.StaffMemberId);
                        releasedSummaries.Add(ToAllocationSummary(outgoing, shift, requester));
                    }
                }
                else
                {
                    var allocs = Allocations.Where(a =>
                        a.StaffMemberId == leave.StaffMemberId
                        && a.Status == AllocationStatus.Confirmed).ToList();

                    foreach (var alloc in allocs)
                    {
                        var shift = Shifts.FirstOrDefault(s => s.Id == alloc.ShiftId);
                        if (shift != null && shift.Date >= leave.StartDate && shift.Date <= leave.EndDate)
                        {
                            alloc.Status = AllocationStatus.Released;
                            alloc.EndedReason = AllocationEndReason.LeaveApproved;
                            alloc.EndedAt = now;

                            var requester = Staff.FirstOrDefault(s => s.Id == leave.StaffMemberId);
                            releasedSummaries.Add(ToAllocationSummary(alloc, shift, requester));
                        }
                    }
                }
            }

            var staff = Staff.FirstOrDefault(s => s.Id == leave.StaffMemberId);
            return Task.FromResult(new DecideLeaveResponse
            {
                LeaveRequest = MapToDto(leave, staff?.FullName ?? string.Empty),
                ReleasedAllocations = releasedSummaries,
                RosterProposalIds = Array.Empty<Guid>()
            });
        }

        public Task<IReadOnlyList<LeaveRequestDto>> GetMyLeaveRequestsAsync(
            LeaveStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            var query = LeaveRequests.Where(l => l.StaffMemberId == CallerId);
            if (status.HasValue)
            {
                query = query.Where(l => l.Status == status.Value);
            }

            var staff = Staff.FirstOrDefault(s => s.Id == CallerId);
            var staffName = staff?.FullName ?? string.Empty;

            var result = query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => MapToDto(l, staffName))
                .ToList();

            return Task.FromResult<IReadOnlyList<LeaveRequestDto>>(result);
        }

        public Task WithdrawLeaveRequestAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var leave = LeaveRequests.FirstOrDefault(l => l.Id == id);
            if (leave == null)
            {
                throw new NotFoundException("LeaveRequest", id);
            }

            if (leave.StaffMemberId != CallerId)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            if (leave.Status != LeaveStatus.Pending)
            {
                throw new IllegalTransitionException("LeaveRequest", leave.Status.ToString(), LeaveStatus.Withdrawn.ToString());
            }

            leave.Status = LeaveStatus.Withdrawn;
            leave.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        private ShiftSummaryDto MapToShiftSummaryDto(TestShift shift, int currentConfirmedCount)
        {
            return new ShiftSummaryDto
            {
                Id = shift.Id,
                WardId = shift.WardId,
                WardName = WardNames.GetValueOrDefault(shift.WardId, "Ward A"),
                Date = shift.Date,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                CrossesMidnight = false,
                RequiredRole = shift.RequiredRole,
                RequiredSkillId = shift.RequiredSkillId,
                HeadcountNeeded = shift.HeadcountNeeded,
                MinimumHeadcount = shift.MinimumHeadcount,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Coverage = CalculateCoverage(shift, currentConfirmedCount)
            };
        }

        private AllocationSummaryDto ToAllocationSummary(TestAllocation alloc, TestShift? shift, TestStaffMember? staff)
        {
            return new AllocationSummaryDto
            {
                AllocationId = alloc.Id,
                ShiftId = alloc.ShiftId,
                StaffMemberId = alloc.StaffMemberId,
                StaffName = staff?.FullName ?? string.Empty,
                WardName = shift != null && WardNames.TryGetValue(shift.WardId, out var wName) ? wName : "Ward A",
                Date = shift?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
                StartTime = shift?.StartTime ?? "08:00",
                EndTime = shift?.EndTime ?? "16:00",
                Status = alloc.Status
            };
        }

        private static ShiftCoverageDto CalculateCoverage(TestShift shift, int confirmedCount)
        {
            var shortfall = Math.Max(0, shift.MinimumHeadcount - confirmedCount);
            CoverageStatus status;
            if (confirmedCount >= shift.HeadcountNeeded) status = CoverageStatus.Adequate;
            else if (confirmedCount >= shift.MinimumHeadcount) status = CoverageStatus.AtMinimum;
            else if (confirmedCount > 0) status = CoverageStatus.Understaffed;
            else status = CoverageStatus.Critical;

            return new ShiftCoverageDto
            {
                ConfirmedCount = confirmedCount,
                HeadcountNeeded = shift.HeadcountNeeded,
                MinimumHeadcount = shift.MinimumHeadcount,
                Status = status,
                ShortfallToMinimum = shortfall
            };
        }

        private static (DateTime Start, DateTime End) GetShiftDateTimeRange(TestShift shift)
        {
            var startParts = shift.StartTime.Split(':');
            var endParts = shift.EndTime.Split(':');
            var startHour = int.Parse(startParts[0]);
            var startMinute = startParts.Length > 1 ? int.Parse(startParts[1]) : 0;
            var endHour = int.Parse(endParts[0]);
            var endMinute = endParts.Length > 1 ? int.Parse(endParts[1]) : 0;

            var start = shift.Date.ToDateTime(new TimeOnly(startHour, startMinute));
            var end = shift.Date.ToDateTime(new TimeOnly(endHour, endMinute));
            if (end <= start)
            {
                end = end.AddDays(1);
            }
            return (start, end);
        }

        public static bool ComputeIsUrgent(LeaveType type, DateOnly startDate)
        {
            if (type == LeaveType.Sick || type == LeaveType.Emergency)
            {
                return true;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var diff = startDate.DayNumber - today.DayNumber;
            return diff <= 2;
        }

        private LeaveRequestDto MapToDto(TestLeaveRequest l, string staffName)
        {
            return new LeaveRequestDto
            {
                Id = l.Id,
                StaffMemberId = l.StaffMemberId,
                StaffName = staffName,
                Type = l.Type,
                IsUrgent = ComputeIsUrgent(l.Type, l.StartDate),
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                Reason = l.Reason,
                Status = l.Status,
                ReviewedByStaffId = l.ReviewedByStaffMemberId,
                ReviewedAt = l.ReviewedByStaffMemberId.HasValue ? l.UpdatedAt : null,
                ReviewNotes = l.ReviewNotes,
                SwapWithStaffMemberId = l.SwapWithStaffMemberId,
                SwapShiftId = l.SwapShiftId,
                CreatedAt = l.CreatedAt
            };
        }
    }

    public class TestStaffMember
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public StaffRole Role { get; set; }
        public bool IsActive { get; set; } = true;
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

    public class TestAllocation
    {
        public Guid Id { get; set; }
        public Guid ShiftId { get; set; }
        public Guid StaffMemberId { get; set; }
        public AllocationStatus Status { get; set; } = AllocationStatus.Confirmed;
        public AllocationSource Source { get; set; } = AllocationSource.Manual;
        public AllocationEndReason? EndedReason { get; set; }
        public Guid? ReplacedByAllocationId { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
    }

    public class TestStaffSkill
    {
        public Guid StaffId { get; set; }
        public Guid SkillId { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ExpiresAt { get; set; }
    }

    public class TestLeaveRequest
    {
        public Guid Id { get; set; }
        public Guid StaffMemberId { get; set; }
        public LeaveType Type { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Reason { get; set; }
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
        public Guid? SwapShiftId { get; set; }
        public Guid? SwapWithStaffMemberId { get; set; }
        public Guid? ReviewedByStaffMemberId { get; set; }
        public string? ReviewNotes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    #endregion
}
