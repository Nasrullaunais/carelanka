using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EmergencyApiQualityTests
{
    private readonly ApiApplication _application;

    public EmergencyApiQualityTests(ApiApplication application) => _application = application;

    private static readonly string[] NonManagerStaff =
    [
        ApiApplication.NurseEmail,
        ApiApplication.DoctorEmail,
        ApiApplication.ReceptionEmail,
        ApiApplication.AdministratorEmail,
        ApiApplication.EquipmentEmail,
        ApiApplication.EquipmentAdministratorEmail,
        ApiApplication.AmbulanceEmail
    ];

    public static TheoryData<string> StaffWhoMayNotDispatch => new(NonManagerStaff);

    [Theory]
    [Trait("id", "EM-TC-01")]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    [InlineData(0.0, 0.0)]
    public async Task A_call_exactly_on_the_coordinate_limits_is_taken(double latitude, double longitude)
    {
        using var patient = await PatientClientAsync();

        using var response = await patient.PostAsJsonAsync(
            "/api/emergency-calls", CallBody(latitude: latitude, longitude: longitude));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await _application.WaitForDispatchProposalAsync(await IdOfAsync(response));
    }

    [Theory]
    [Trait("id", "EM-TC-02")]
    [InlineData(90.000001, 79.861244, "latitude")]
    [InlineData(-90.000001, 79.861244, "latitude")]
    [InlineData(6.927079, 180.000001, "longitude")]
    [InlineData(6.927079, -180.000001, "longitude")]
    public async Task A_call_just_outside_the_coordinate_limits_is_refused(
        double latitude, double longitude, string field)
    {
        using var patient = await PatientClientAsync();

        using var response = await patient.PostAsJsonAsync(
            "/api/emergency-calls", CallBody(latitude: latitude, longitude: longitude));

        await AssertValidationProblemAsync(response, field);
    }

    [Theory]
    [Trait("id", "EM-TC-03")]
    [InlineData("details", 1000, true)]
    [InlineData("details", 1001, false)]
    [InlineData("caller_name", 200, true)]
    [InlineData("caller_name", 201, false)]
    [InlineData("caller_phone", 20, true)]
    [InlineData("caller_phone", 21, false)]
    public async Task Text_fields_are_taken_up_to_their_limit_and_refused_one_past_it(
        string field, int length, bool accepted)
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var text = new string('7', length);
        var body = field switch
        {
            "details" => CallBody(patientIsCaller: false, details: text),
            "caller_name" => CallBody(patientIsCaller: false, callerName: text),
            _ => CallBody(patientIsCaller: false, callerPhone: text)
        };

        using var response = await manager.PostAsJsonAsync("/api/emergency-calls", body);

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await _application.WaitForDispatchProposalAsync(await IdOfAsync(response));
        }
        else
        {
            await AssertValidationProblemAsync(response, field);
        }
    }

    [Theory]
    [Trait("id", "EM-TC-04")]
    [InlineData(0.0, true)]
    [InlineData(50_000.0, true)]
    [InlineData(99_999_999.99, true)]
    [InlineData(-0.01, false)]
    [InlineData(100_000_000.0, false)]
    [InlineData(1e12, false)]
    public async Task Location_accuracy_is_checked_at_both_ends(double accuracy, bool accepted)
    {
        using var patient = await PatientClientAsync();

        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", CallBody(accuracy: accuracy));

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await _application.WaitForDispatchProposalAsync(await IdOfAsync(response));
        }
        else
        {
            await AssertValidationProblemAsync(response, "location_accuracy_metres");
        }
    }

    [Theory]
    [Trait("id", "EM-TC-04b")]
    [InlineData(99_999_999.99, true)]
    [InlineData(100_000_000.0, false)]
    public async Task Moving_a_call_checks_the_new_location_accuracy_too(double accuracy, bool accepted)
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var created = await manager.PostAsJsonAsync("/api/emergency-calls", CallBody(patientIsCaller: false));
        var callId = await IdOfAsync(created);
        await _application.WaitForDispatchProposalAsync(callId);

        using var response = await manager.PatchAsJsonAsync($"/api/emergency-calls/{callId}", new
        {
            latitude = 6.93,
            longitude = 79.85,
            location_accuracy_metres = accuracy
        });

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await _application.WaitForDispatchProposalAsync(callId);
        }
        else
        {
            await AssertValidationProblemAsync(response, "location_accuracy_metres");
        }
    }

    [Fact]
    [Trait("id", "EM-TC-05")]
    public async Task An_empty_idempotency_key_is_refused()
    {
        using var patient = await PatientClientAsync();

        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", CallBody(key: Guid.Empty));

        await AssertValidationProblemAsync(response, "idempotency_key");
    }

    [Fact]
    [Trait("id", "EM-TC-06")]
    public async Task Without_a_login_every_emergency_route_answers_401()
    {
        using var anonymous = _application.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await anonymous.PostAsJsonAsync("/api/emergency-calls", CallBody()),
            await anonymous.GetAsync("/api/emergency-calls"),
            await anonymous.GetAsync($"/api/emergency-calls/{id}"),
            await anonymous.GetAsync("/api/ambulances"),
            await anonymous.GetAsync("/api/fleet-map"),
            await anonymous.GetAsync("/api/dispatch-proposals"),
            await anonymous.PostAsync($"/api/dispatch-proposals/{id}/confirm", null),
            await anonymous.GetAsync("/api/me/dispatches/active"),
            await anonymous.GetAsync("/api/me/emergency-calls"),
            await anonymous.GetAsync("/api/reports/emergency/response-times")
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [Trait("id", "EM-TC-07")]
    [MemberData(nameof(StaffWhoMayNotDispatch))]
    public async Task Only_the_duty_manager_sees_the_call_board_and_fleet_map(string email)
    {
        using var staff = await StaffClientAsync(email);

        using var board = await staff.GetAsync("/api/emergency-calls");
        using var map = await staff.GetAsync("/api/fleet-map");
        using var proposals = await staff.GetAsync("/api/dispatch-proposals");
        using var reports = await staff.GetAsync("/api/reports/emergency/response-times");

        Assert.Equal(HttpStatusCode.Forbidden, board.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, map.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, proposals.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reports.StatusCode);
    }

    [Fact]
    [Trait("id", "EM-TC-08")]
    public async Task A_patient_cannot_track_or_cancel_another_patients_call()
    {
        using var owner = await PatientClientAsync();
        using var stranger = await PatientClientAsync();
        using var created = await owner.PostAsJsonAsync("/api/emergency-calls", CallBody());
        var callId = await IdOfAsync(created);
        await _application.WaitForDispatchProposalAsync(callId);

        using var tracking = await stranger.GetAsync($"/api/me/emergency-calls/{callId}/tracking");
        using var cancel = await stranger.PostAsJsonAsync(
            $"/api/me/emergency-calls/{callId}/cancel", new { reason = "Not me" });
        using var askToCancel = await stranger.PostAsJsonAsync(
            $"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "Not me" });

        Assert.Equal(HttpStatusCode.Forbidden, tracking.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, askToCancel.StatusCode);

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = await db.EmergencyCalls.AsNoTracking().SingleAsync(row => row.Id == callId);
        Assert.Equal(CallStatus.Received, call.Status);
        Assert.Null(call.CancellationRequestStatus);
    }

    [Fact]
    [Trait("id", "EM-TC-09")]
    public async Task A_patient_cannot_set_the_priority_of_their_own_call()
    {
        using var patient = await PatientClientAsync();

        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = true,
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 10,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid(),
            priority = "critical"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static TheoryData<string, string> BadReportRanges => new()
    {
        { "response-times", "" },
        { "response-times", "?from=2026-10-01" },
        { "response-times", "?to=2026-10-05" },
        { "response-times", "?from=2026-10-05&to=2026-10-01" },
        { "response-times", "?from=0001-01-01&to=2026-10-05" },
        { "response-times", "?from=2026-10-01&to=9999-12-31" },
        { "fleet-utilisation", "" },
        { "fleet-utilisation", "?from=0001-01-01&to=2026-10-05" },
        { "agent-performance", "" },
        { "agent-performance", "?from=2026-10-01&to=9999-12-31" }
    };

    [Theory]
    [Trait("id", "EM-TC-10")]
    [MemberData(nameof(BadReportRanges))]
    public async Task A_report_with_a_missing_or_impossible_date_range_is_refused(string report, string query)
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);

        using var response = await manager.GetAsync($"/api/reports/emergency/{report}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [Trait("id", "EM-TC-10b")]
    [InlineData("response-times", "?from=2026-10-01&to=2026-10-05&priority=critical")]
    [InlineData("response-times", "?from=2026-10-05&to=2026-10-05")]
    [InlineData("fleet-utilisation", "?from=2026-10-01&to=2026-10-05")]
    [InlineData("agent-performance", "?from=2026-10-01&to=2026-10-05")]
    public async Task A_report_with_a_real_date_range_is_returned(string report, string query)
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);

        using var response = await manager.GetAsync($"/api/reports/emergency/{report}{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("id", "EM-AI-15")]
    public async Task Nobody_but_the_duty_manager_can_send_the_agents_recommendation()
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateReadyAmbulanceAsync(manager);
        using var created = await manager.PostAsJsonAsync("/api/emergency-calls", CallBody(patientIsCaller: false));
        var callId = await IdOfAsync(created);
        await _application.WaitForDispatchProposalAsync(callId);
        var proposalId = await OpenProposalIdAsync(callId);

        var outsiders = new List<HttpClient> { _application.CreateClient(), await PatientClientAsync() };
        foreach (var email in NonManagerStaff)
        {
            outsiders.Add(await StaffClientAsync(email));
        }

        foreach (var outsider in outsiders)
        {
            var expected = outsider.DefaultRequestHeaders.Authorization is null
                ? HttpStatusCode.Unauthorized
                : HttpStatusCode.Forbidden;

            using var confirm = await outsider.PostAsync($"/api/dispatch-proposals/{proposalId}/confirm", null);
            using var approve = await outsider.PostAsJsonAsync(
                $"/api/dispatch-proposals/{proposalId}/approve", new { notes = "QM" });
            using var reject = await outsider.PostAsJsonAsync(
                $"/api/dispatch-proposals/{proposalId}/reject", new { reason = "no_longer_needed" });

            Assert.Equal(expected, confirm.StatusCode);
            Assert.Equal(expected, approve.StatusCode);
            Assert.Equal(expected, reject.StatusCode);
            outsider.Dispose();
        }

        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var proposal = await db.DispatchProposals.AsNoTracking().SingleAsync(row => row.Id == proposalId);
            Assert.Equal(DispatchProposalStatus.PendingConfirmation, proposal.Status);
            Assert.Null(proposal.ResultingDispatchId);
            Assert.False(await db.Dispatches.AnyAsync(row => row.EmergencyCallId == callId));
        }

        using var managerReject = await manager.PostAsJsonAsync(
            $"/api/dispatch-proposals/{proposalId}/reject", new { reason = "no_longer_needed" });
        Assert.Equal(HttpStatusCode.OK, managerReject.StatusCode);
        using var retire = await manager.PostAsJsonAsync(
            $"/api/ambulances/{ambulanceId}/retire", new { reason = "QM test finished" });
        Assert.True(retire.IsSuccessStatusCode, $"Retiring the test ambulance returned {retire.StatusCode}.");
    }

    [Theory]
    [Trait("id", "EM-AI-22")]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(300, 5)]
    [InlineData(301, 6)]
    [InlineData(359, 6)]
    public async Task The_agent_rounds_road_time_up_like_the_ambulance_list_and_tracking(int driveSeconds, int minutes)
    {
        using var scope = _application.Services.CreateScope();
        var ambulance = new AmbulanceLocation(Guid.NewGuid(), 6.92m, 79.86m);
        var tools = new DispatchAgentTools(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            scope.ServiceProvider.GetRequiredService<IAmbulanceEligibilityService>(),
            new FixedRoadTime(driveSeconds));

        var byAmbulance = await tools.GetRouteMinutesAsync([ambulance], 6.93m, 79.86m);

        Assert.Equal(minutes, byAmbulance[ambulance.Id]);
    }

    private sealed class FixedRoadTime(int driveSeconds) : IAmbulanceDistanceService
    {
        public Task<DistanceMeasurement> MeasureAsync(
            IReadOnlyCollection<AmbulanceLocation> ambulances,
            decimal destinationLatitude,
            decimal destinationLongitude,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DistanceMeasurement(
                ambulances.ToDictionary(ambulance => ambulance.Id, _ => new AmbulanceTravel(3.5, driveSeconds)),
                IsStraightLine: false));
    }

    private async Task<Guid> OpenProposalIdAsync(Guid callId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var proposal = await db.DispatchProposals.AsNoTracking()
            .Where(row => row.EmergencyCallId == callId)
            .OrderByDescending(row => row.CreatedAt)
            .FirstAsync();
        Assert.Equal(DispatchProposalStatus.PendingConfirmation, proposal.Status);
        return proposal.Id;
    }

    private async Task<Guid> CreateReadyAmbulanceAsync(HttpClient manager)
    {
        using var created = await manager.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = $"QM-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            current_latitude = 6.927079,
            current_longitude = 79.861244
        });
        created.EnsureSuccessStatusCode();
        var ambulanceId = await IdOfAsync(created);

        var crewIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var passwords = scope.ServiceProvider.GetRequiredService<CareLanka.Api.Services.Common.IPasswordService>();
            foreach (var crewId in crewIds)
            {
                db.StaffMembers.Add(new StaffMember
                {
                    Id = crewId,
                    Email = $"qm-crew-{crewId:N}@carelanka.invalid",
                    PasswordHash = passwords.Hash(ApiApplication.Password),
                    FirstName = "QM",
                    LastName = "Crew",
                    Role = StaffRole.AmbulanceCrew,
                    IsActive = true
                });
            }

            await db.SaveChangesAsync();
        }

        foreach (var crewId in crewIds)
        {
            using var assigned = await manager.PostAsJsonAsync(
                $"/api/ambulances/{ambulanceId}/crew", new { staff_member_id = crewId });
            assigned.EnsureSuccessStatusCode();
        }

        return ambulanceId;
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _),
            $"Expected a validation error on '{field}'.");
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static object CallBody(
        bool patientIsCaller = true,
        double latitude = 6.927079,
        double longitude = 79.861244,
        double accuracy = 10,
        Guid? key = null,
        string? details = null,
        string? callerName = null,
        string? callerPhone = null) => new
    {
        patient_is_caller = patientIsCaller,
        caller_name = callerName,
        caller_phone = callerPhone,
        latitude,
        longitude,
        location_accuracy_metres = accuracy,
        location_captured_at = DateTimeOffset.UtcNow,
        idempotency_key = key ?? Guid.NewGuid(),
        details
    };

    private async Task<HttpClient> PatientClientAsync()
    {
        var client = _application.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"qm-emergency-{Guid.NewGuid():N}",
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
}
