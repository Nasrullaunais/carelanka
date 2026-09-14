using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
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
            phone_number = $"+947{Random.Shared.Next(10000000, 99999999)}",
            password = ApiApplication.Password,
            full_name = "Emergency Caller"
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
}
