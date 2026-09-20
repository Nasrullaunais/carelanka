using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EmergencyCallEndpointTests
{
    private readonly ApiApplication _application;

    public EmergencyCallEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Patient_submission_is_stored_once_and_appears_on_the_duty_manager_call_board()
    {
        using var patient = await PatientClientAsync();
        var key = Guid.NewGuid();

        using var created = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = true,
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 12.5,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = key,
            details = "Collapsed near the bus stop"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var callId = createdBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("high", createdBody.RootElement.GetProperty("priority").GetString());
        Assert.Equal("received", createdBody.RootElement.GetProperty("status").GetString());

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.Equal(1, await db.EmergencyCalls.CountAsync(call => call.Id == callId));

        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var board = await manager.GetAsync("/api/emergency-calls?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        using var boardBody = JsonDocument.Parse(await board.Content.ReadAsStringAsync());
        Assert.Contains(boardBody.RootElement.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == callId);
    }

    [Fact]
    public async Task A_patient_call_can_be_manually_dispatched_and_handed_over_without_AI()
    {
        using var patient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var callResponse = await patient.PostAsJsonAsync("/api/emergency-calls", Request(Guid.NewGuid(), true));
        using var callBody = JsonDocument.Parse(await callResponse.Content.ReadAsStringAsync());
        var callId = callBody.RootElement.GetProperty("id").GetGuid();
        var ready = await CreateReadyAmbulanceAsync(manager);
        using var crew = await StaffClientAsync(ready.FirstCrewEmail);
        var ambulanceId = ready.AmbulanceId;

        using var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulanceId });
        Assert.Equal(HttpStatusCode.Created, dispatched.StatusCode);
        using var dispatchBody = JsonDocument.Parse(await dispatched.Content.ReadAsStringAsync());
        var dispatchId = dispatchBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("assigned", dispatchBody.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, dispatchBody.RootElement.GetProperty("crew_staff_ids").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "en_route_to_scene" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "at_scene" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "transporting_to_hospital" })).StatusCode);
        using var handover = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/handover", new { notes = "Handed to triage" });
        Assert.Equal(HttpStatusCode.OK, handover.StatusCode);
        using var handoverBody = JsonDocument.Parse(await handover.Content.ReadAsStringAsync());
        Assert.Equal("handed_over", handoverBody.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Concurrent_dispatches_for_one_ambulance_leave_exactly_one_assigned()
    {
        using var firstPatient = await PatientClientAsync();
        using var secondPatient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = (await CreateReadyAmbulanceAsync(manager)).AmbulanceId;
        var firstCall = await CreatePatientCallAsync(firstPatient);
        var secondCall = await CreatePatientCallAsync(secondPatient);

        var attempts = await Task.WhenAll(
            manager.PostAsJsonAsync($"/api/emergency-calls/{firstCall}/dispatch", new { ambulance_id = ambulanceId }),
            manager.PostAsJsonAsync($"/api/emergency-calls/{secondCall}/dispatch", new { ambulance_id = ambulanceId }));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], attempts.Select(x => x.StatusCode).Order().ToArray());
        foreach (var attempt in attempts) attempt.Dispose();
    }

    [Fact]
    public async Task Caller_tracking_is_narrow_and_location_is_owned_by_responding_crew()
    {
        using var patient = await PatientClientAsync();
        using var otherPatient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreatePatientCallAsync(patient);
        var ready = await CreateReadyAmbulanceAsync(manager);
        using var crew = await StaffClientAsync(ready.FirstCrewEmail);
        var ambulanceId = ready.AmbulanceId;
        using var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulanceId });
        dispatched.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await crew.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/location", new { latitude = 6.930m, longitude = 79.865m })).StatusCode);
        using var tracking = await patient.GetAsync($"/api/me/emergency-calls/{callId}/tracking");
        tracking.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await tracking.Content.ReadAsStringAsync());
        Assert.Equal(6.930m, body.RootElement.GetProperty("ambulance_latitude").GetDecimal());
        Assert.False(body.RootElement.TryGetProperty("crew", out _));
        Assert.False(body.RootElement.TryGetProperty("details", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await otherPatient.GetAsync($"/api/me/emergency-calls/{callId}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancel", new { reason = "No longer needed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "No longer needed" })).StatusCode);
    }

    [Fact]
    public async Task Location_reporting_rejects_invalid_coordinates_and_crew_outside_the_response_unit()
    {
        using var patient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var unrelatedCrew = await StaffClientAsync(ApiApplication.AmbulanceEmail);
        var callId = await CreatePatientCallAsync(patient);
        var ready = await CreateReadyAmbulanceAsync(manager);
        using var respondingCrew = await StaffClientAsync(ready.FirstCrewEmail);
        using var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ready.AmbulanceId });
        dispatched.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await respondingCrew.PostAsJsonAsync($"/api/ambulances/{ready.AmbulanceId}/location", new { latitude = 91, longitude = 79.861244 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedCrew.PostAsJsonAsync($"/api/ambulances/{ready.AmbulanceId}/location", new { latitude = 6.927079, longitude = 79.861244 })).StatusCode);
    }

    [Fact]
    public async Task Tracking_marks_an_old_ambulance_position_as_stale()
    {
        using var patient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreatePatientCallAsync(patient);
        var ready = await CreateReadyAmbulanceAsync(manager);
        using var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ready.AmbulanceId });
        dispatched.EnsureSuccessStatusCode();
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var ambulance = await db.Ambulances.SingleAsync(item => item.Id == ready.AmbulanceId);
            ambulance.LocationUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-6);
            await db.SaveChangesAsync();
        }

        using var tracking = await patient.GetAsync($"/api/me/emergency-calls/{callId}/tracking");
        tracking.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await tracking.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("ambulance_location_is_stale").GetBoolean());
    }

    [Fact]
    public async Task Caller_can_cancel_before_any_dispatch()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        using var cancelled = await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancel", new { reason = "Created by mistake" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var body = JsonDocument.Parse(await cancelled.Content.ReadAsStringAsync());
        Assert.Equal("cancelled", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Duty_manager_can_approve_a_pending_pre_arrival_cancellation_request()
    {
        using var patient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreatePatientCallAsync(patient);
        var ambulanceId = (await CreateReadyAmbulanceAsync(manager)).AmbulanceId;
        using var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulanceId });
        dispatched.EnsureSuccessStatusCode();
        using var requested = await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "No longer needed" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);

        using var pending = await manager.GetAsync("/api/emergency-cancellation-requests?status=pending");
        pending.EnsureSuccessStatusCode();
        using var pendingBody = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        Assert.Contains(pendingBody.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("emergency_call_id").GetGuid() == callId);

        using var approved = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancellation-request/approve", new { notes = "Caller confirmed" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var body = JsonDocument.Parse(await approved.Content.ReadAsStringAsync());
        Assert.Equal("approved", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Caller confirmed", body.RootElement.GetProperty("review_notes").GetString());

        using var tracking = await patient.GetAsync($"/api/me/emergency-calls/{callId}/tracking");
        Assert.Equal(HttpStatusCode.NotFound, tracking.StatusCode);
    }

    [Fact]
    public async Task Duty_manager_cannot_approve_cancellation_after_the_crew_is_at_scene()
    {
        using var patient = await PatientClientAsync();
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreatePatientCallAsync(patient);
        var ready = await CreateReadyAmbulanceAsync(manager);
        using var crew = await StaffClientAsync(ready.FirstCrewEmail);
        var ambulanceId = ready.AmbulanceId;
        using var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulanceId });
        using var dispatchBody = JsonDocument.Parse(await dispatched.Content.ReadAsStringAsync());
        var dispatchId = dispatchBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "en_route_to_scene" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "at_scene" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "No longer needed" })).StatusCode);

        using var approved = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancellation-request/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
    }

    [Theory]
    [InlineData(null, 79.861244, 5.0)]
    [InlineData(6.927079, null, 5.0)]
    [InlineData(91.0, 79.861244, 5.0)]
    [InlineData(6.927079, -181.0, 5.0)]
    [InlineData(6.927079, 79.861244, -0.1)]
    public async Task Missing_or_invalid_coordinates_return_standard_validation_problem(
        double? latitude,
        double? longitude,
        double? accuracy)
    {
        using var patient = await PatientClientAsync();

        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = true,
            latitude,
            longitude,
            location_accuracy_metres = accuracy,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Bad Request", body.RootElement.GetProperty("title").GetString());
        Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateObject());
    }

    [Fact]
    public async Task Required_intake_fields_are_rejected_but_optional_report_fields_may_be_null()
    {
        using var patient = await PatientClientAsync();
        using var missingRequired = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            latitude = 6.927079,
            longitude = 79.861244
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingRequired.StatusCode);
        using var invalidBody = JsonDocument.Parse(await missingRequired.Content.ReadAsStringAsync());
        var errors = invalidBody.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("patient_is_caller", out _));
        Assert.True(errors.TryGetProperty("location_accuracy_metres", out _));
        Assert.True(errors.TryGetProperty("location_captured_at", out _));
        Assert.True(errors.TryGetProperty("idempotency_key", out _));

        using var valid = await patient.PostAsJsonAsync("/api/emergency-calls", Request(
            Guid.NewGuid(), patientIsCaller: true));
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        using var validBody = JsonDocument.Parse(await valid.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, validBody.RootElement.GetProperty("patient_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, validBody.RootElement.GetProperty("caller_name").ValueKind);
        Assert.Equal(JsonValueKind.Null, validBody.RootElement.GetProperty("caller_phone").ValueKind);
        Assert.Equal(JsonValueKind.Null, validBody.RootElement.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task Caller_identity_comes_from_the_patient_JWT_and_client_cannot_override_it()
    {
        using var patient = await PatientClientAsync();
        var principal = await patient.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var principalId = principal.GetProperty("id").GetGuid();

        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            caller_user_id = Guid.NewGuid(),
            patient_is_caller = false,
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 8.0,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid(),
            details = "Helping another person"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(principalId, body.RootElement.GetProperty("caller_user_id").GetGuid());
        Assert.False(body.RootElement.GetProperty("patient_is_caller").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("patient_id").ValueKind);
    }

    [Fact]
    public async Task Staff_can_log_a_phone_call_but_cannot_use_the_patient_own_call_list()
    {
        using var staff = await StaffClientAsync(ApiApplication.ReceptionEmail);
        using var created = await staff.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = false,
            caller_name = "Nimal Silva",
            caller_phone = "+94771234567",
            latitude = 6.9,
            longitude = 79.8,
            location_accuracy_metres = 20,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("caller_user_id").ValueKind);

        using var ownCalls = await staff.GetAsync("/api/me/emergency-calls");
        Assert.Equal(HttpStatusCode.Forbidden, ownCalls.StatusCode);
    }

    [Fact]
    public async Task Repeating_the_same_patient_idempotency_key_returns_the_original_call()
    {
        using var patient = await PatientClientAsync();
        var key = Guid.NewGuid();

        using var first = await patient.PostAsJsonAsync("/api/emergency-calls", Request(
            key, patientIsCaller: true, details: "Original report"));
        using var second = await patient.PostAsJsonAsync("/api/emergency-calls", Request(
            key, patientIsCaller: true, details: "Changed retry body"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(firstBody.RootElement.GetProperty("id").GetGuid(),
            secondBody.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Original report", secondBody.RootElement.GetProperty("details").GetString());

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.Equal(1, await db.EmergencyCalls.CountAsync(call => call.IdempotencyKey == key));
    }

    [Fact]
    public async Task Patient_priority_defaults_to_high_and_only_a_duty_manager_may_set_or_update_it()
    {
        using var patient = await PatientClientAsync();
        using var forbiddenCreate = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = true,
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 5,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid(),
            priority = "critical"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        using var defaulted = await patient.PostAsJsonAsync("/api/emergency-calls", Request(
            Guid.NewGuid(), patientIsCaller: true));
        using var defaultBody = JsonDocument.Parse(await defaulted.Content.ReadAsStringAsync());
        var id = defaultBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("high", defaultBody.RootElement.GetProperty("priority").GetString());

        using var patientUpdate = await patient.PatchAsJsonAsync($"/api/emergency-calls/{id}", new
        {
            priority = "critical"
        });
        Assert.Equal(HttpStatusCode.Forbidden, patientUpdate.StatusCode);

        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var updated = await manager.PatchAsJsonAsync($"/api/emergency-calls/{id}", new
        {
            priority = "critical",
            details = "Manager-confirmed urgent report",
            latitude = 6.91,
            longitude = 79.87
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal("critical", updatedBody.RootElement.GetProperty("priority").GetString());
        Assert.Equal("Manager-confirmed urgent report", updatedBody.RootElement.GetProperty("details").GetString());
        Assert.Equal(6.91m, updatedBody.RootElement.GetProperty("latitude").GetDecimal());
    }

    [Fact]
    public async Task Own_call_list_and_detail_are_strictly_scoped_to_the_patient_caller()
    {
        using var firstPatient = await PatientClientAsync();
        using var secondPatient = await PatientClientAsync();
        using var created = await firstPatient.PostAsJsonAsync("/api/emergency-calls", Request(
            Guid.NewGuid(), patientIsCaller: false, details: "Bystander-owned call"));
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();

        using var firstList = await firstPatient.GetAsync("/api/me/emergency-calls?page=1&pageSize=100");
        using var firstListBody = JsonDocument.Parse(await firstList.Content.ReadAsStringAsync());
        Assert.Contains(firstListBody.RootElement.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == id);

        using var secondList = await secondPatient.GetAsync("/api/me/emergency-calls?page=1&pageSize=100");
        using var secondListBody = JsonDocument.Parse(await secondList.Content.ReadAsStringAsync());
        Assert.DoesNotContain(secondListBody.RootElement.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == id);

        using var forbidden = await secondPatient.GetAsync($"/api/emergency-calls/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var allowed = await firstPatient.GetAsync($"/api/emergency-calls/{id}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Dispatcher_board_supports_priority_search_status_and_pagination_filters()
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var marker = $"board-{Guid.NewGuid():N}";
        var firstId = await CreateStaffCallAsync(manager, marker, "critical");
        var secondId = await CreateStaffCallAsync(manager, marker, "low");

        using var filtered = await manager.GetAsync(
            $"/api/emergency-calls?search={marker}&status=received&priority=critical&page=1&pageSize=1&sortBy=priority&sortDir=desc");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var filteredBody = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Equal(1, filteredBody.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(firstId, filteredBody.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());

        using var page = await manager.GetAsync(
            $"/api/emergency-calls?search={marker}&page=2&pageSize=1&sortBy=priority&sortDir=desc");
        using var pageBody = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        Assert.Equal(2, pageBody.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(secondId, pageBody.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());

        using var patient = await PatientClientAsync();
        using var forbidden = await patient.GetAsync("/api/emergency-calls");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Invalid_dispatcher_query_and_partial_coordinate_update_use_validation_problem_details()
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var id = await CreateStaffCallAsync(manager, $"validate-{Guid.NewGuid():N}", "high");

        using var invalidList = await manager.GetAsync("/api/emergency-calls?page=0&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidList.StatusCode);
        Assert.Equal("application/problem+json", invalidList.Content.Headers.ContentType?.MediaType);

        using var invalidUpdate = await manager.PatchAsJsonAsync($"/api/emergency-calls/{id}", new
        {
            latitude = 6.8
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        Assert.Equal("application/problem+json", invalidUpdate.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpClient> PatientClientAsync()
    {
        var client = _application.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"emergency-{Guid.NewGuid():N}",
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AuthorizeFromAsync(client, response);
        return client;
    }

    private async Task<HttpClient> StaffClientAsync(string email)
    {
        var client = _application.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AuthorizeFromAsync(client, response);
        return client;
    }

    private static async Task AuthorizeFromAsync(HttpClient client, HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
    }

    private static object Request(
        Guid key,
        bool patientIsCaller,
        string? details = null) => new
    {
        patient_is_caller = patientIsCaller,
        latitude = 6.927079,
        longitude = 79.861244,
        location_accuracy_metres = 10,
        location_captured_at = DateTimeOffset.UtcNow,
        idempotency_key = key,
        details
    };

    private static async Task<Guid> CreatePatientCallAsync(HttpClient patient)
    {
        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", Request(Guid.NewGuid(), true));
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateStaffCallAsync(
        HttpClient manager,
        string marker,
        string priority)
    {
        using var response = await manager.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = false,
            caller_name = marker,
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 10,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid(),
            priority
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<ReadyAmbulance> CreateReadyAmbulanceAsync(HttpClient manager)
    {
        using var created = await manager.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = $"WP-DSP-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            current_latitude = 6.927079, current_longitude = 79.861244
        });
        created.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var ambulanceId = body.RootElement.GetProperty("id").GetGuid();
        Guid firstCrewId;
        Guid secondCrewId;
        var firstCrewEmail = $"crew-{Guid.NewGuid():N}@carelanka.invalid";
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<CareLanka.Api.Services.Common.IPasswordService>();
            firstCrewId = Guid.NewGuid();
            secondCrewId = Guid.NewGuid();
            db.StaffMembers.Add(new StaffMember { Id = firstCrewId, Email = firstCrewEmail, PasswordHash = passwords.Hash(ApiApplication.Password), FirstName = "First", LastName = "Crew", Role = StaffRole.AmbulanceCrew, IsActive = true });
            db.StaffMembers.Add(new StaffMember { Id = secondCrewId, Email = $"crew-{secondCrewId:N}@carelanka.invalid", PasswordHash = passwords.Hash(ApiApplication.Password), FirstName = "Second", LastName = "Crew", Role = StaffRole.AmbulanceCrew, IsActive = true });
            await db.SaveChangesAsync();
        }
        foreach (var crewId in new[] { firstCrewId, secondCrewId })
        {
            using var assigned = await manager.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new { staff_member_id = crewId });
            assigned.EnsureSuccessStatusCode();
        }
        return new ReadyAmbulance(ambulanceId, firstCrewEmail);
    }

    private sealed record ReadyAmbulance(Guid AmbulanceId, string FirstCrewEmail);
}
