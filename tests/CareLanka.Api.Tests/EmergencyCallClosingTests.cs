using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EmergencyCallClosingTests
{
    private readonly ApiApplication _application;

    public EmergencyCallClosingTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Crew_can_end_a_run_at_the_scene_without_taking_anyone_to_hospital()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await ReachSceneAsync(crew, dispatchId);

        var closed = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/close-at-scene", new
        {
            outcome = "treated_at_scene",
            notes = "  Dressed a cut; no transport needed.  "
        });

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var body = await ReadAsync(closed);
        Assert.Equal("closed_at_scene", body.GetProperty("status").GetString());
        Assert.Equal("completed", body.GetProperty("call_status").GetString());
        Assert.Equal("treated_at_scene", body.GetProperty("call_outcome").GetString());
        var call = await CallAsync(run.CallId);
        Assert.Equal(EmergencyCallOutcome.TreatedAtScene, call.Outcome);
        Assert.Equal("Dressed a cut; no transport needed.", call.OutcomeNotes);
        Assert.False(call.Transported);
        Assert.NotNull(call.ClosedAt);
        Assert.Equal(AmbulanceStatus.Available, (await AmbulanceAsync(run.AmbulanceId)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await crew.GetAsync("/api/me/dispatches/active")).StatusCode);
    }

    [Fact]
    public async Task A_run_closed_at_the_scene_sends_no_pre_admission()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await ReachSceneAsync(crew, dispatchId);
        await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/close-at-scene", new { outcome = "refused_transport" });

        var gateway = new RecordingGateway();
        using (var scope = _application.Services.CreateScope())
        {
            var processor = new PreAdmissionProcessor(
                scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
                gateway,
                TimeProvider.System,
                Options.Create(new EmergencyOptions { PreAdmission = { BatchSize = 1000 } }));
            await processor.SendDueAsync();
        }

        using var check = _application.Services.CreateScope();
        var notice = await check.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .PreAdmissionNotices.AsNoTracking().SingleAsync(x => x.EmergencyCallId == run.CallId);
        Assert.Equal(PreAdmissionStatus.Failed, notice.Status);
        Assert.Equal("not_transported", notice.FailureReason);
        Assert.DoesNotContain(gateway.Sent, request => request.DispatchId == dispatchId);
    }

    [Fact]
    public async Task A_run_can_only_be_closed_at_the_scene_once_the_crew_is_there()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null);

        var tooEarly = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/close-at-scene", new { outcome = "patient_not_found" });

        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal(DispatchStatus.Acknowledged, (await LoadDispatchAsync(dispatchId)).Status);
    }

    [Theory]
    [InlineData("transported")]
    [InlineData("false_alarm")]
    [InlineData(null)]
    public async Task Closing_at_the_scene_needs_an_outcome_that_happens_at_a_scene(string? outcome)
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await ReachSceneAsync(crew, dispatchId);

        var response = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/close-at-scene", new { outcome });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(DispatchStatus.AtScene, (await LoadDispatchAsync(dispatchId)).Status);
    }

    [Fact]
    public async Task Only_the_responding_crew_can_close_their_run()
    {
        var run = await SeedRunAsync();
        var other = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await ReachSceneAsync(crew, dispatchId);
        using var outsider = await ClientAsync(other.CrewEmails[0]);

        var response = await outsider.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/close-at-scene", new { outcome = "treated_at_scene" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Duty_manager_closing_a_call_calls_off_the_ambulance_on_its_way()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var closed = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/cancel", new
        {
            outcome = "false_alarm",
            notes = "Caller rang back, prank"
        });

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var body = await ReadAsync(closed);
        Assert.Equal("cancelled", body.GetProperty("status").GetString());
        Assert.Equal("false_alarm", body.GetProperty("outcome").GetString());
        Assert.Equal("Caller rang back, prank", body.GetProperty("outcome_notes").GetString());
        Assert.Equal(JsonValueKind.String, body.GetProperty("closed_at").ValueKind);
        var dispatch = await LoadDispatchAsync(dispatchId);
        Assert.Equal(DispatchStatus.Cancelled, dispatch.Status);
        Assert.Equal("Caller rang back, prank", dispatch.CancellationReason);
        Assert.Equal(AmbulanceStatus.Available, (await AmbulanceAsync(run.AmbulanceId)).Status);
        Assert.Equal(CallStatus.Cancelled, (await CallAsync(run.CallId)).Status);
    }

    [Fact]
    public async Task Duty_manager_closing_a_waiting_call_withdraws_its_recommendation()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var closed = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancel", new { outcome = "duplicate_call" });

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        using var scope = _application.Services.CreateScope();
        var proposals = await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .DispatchProposals.AsNoTracking().Where(x => x.EmergencyCallId == callId).ToListAsync();
        Assert.DoesNotContain(proposals, proposal => proposal.Status is DispatchProposalStatus.Pending
            or DispatchProposalStatus.PendingConfirmation or DispatchProposalStatus.PendingApproval);
    }

    [Fact]
    public async Task Duty_manager_cannot_close_a_call_once_the_crew_has_reached_the_patient()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await ReachSceneAsync(crew, dispatchId);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var response = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/cancel", new { outcome = "no_longer_needed" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_emg_017", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Equal(DispatchStatus.AtScene, (await LoadDispatchAsync(dispatchId)).Status);
        Assert.Null((await CallAsync(run.CallId)).Outcome);
    }

    [Fact]
    public async Task A_closed_call_cannot_be_closed_again()
    {
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancel", new { outcome = "false_alarm" });

        var again = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancel", new { outcome = "duplicate_call" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_emg_011", (await ReadAsync(again)).GetProperty("code").GetString());
        Assert.Equal(EmergencyCallOutcome.FalseAlarm, (await CallAsync(callId)).Outcome);
    }

    [Theory]
    [InlineData("transported")]
    [InlineData("treated_at_scene")]
    [InlineData(null)]
    public async Task Closing_a_call_needs_a_cancellation_outcome(string? outcome)
    {
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var response = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancel", new { outcome });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CallStatus.Received, (await CallAsync(callId)).Status);
    }

    [Fact]
    public async Task Only_a_duty_manager_can_close_a_call()
    {
        var run = await SeedRunAsync();
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var response = await crew.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/cancel", new { outcome = "false_alarm" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Closing_a_call_answers_a_cancellation_request_the_patient_was_waiting_on()
    {
        using var patient = await PatientClientAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreatePatientCallAsync(patient);
        var run = await SeedRunAsync();
        await DispatchAsync(run with { CallId = callId });
        await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "Took a taxi" });

        var closed = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancel", new { outcome = "caller_cancelled" });

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var call = await CallAsync(callId);
        Assert.Equal(CancellationRequestStatus.Approved, call.CancellationRequestStatus);
        var tracking = await ReadAsync(await patient.GetAsync($"/api/me/emergency-calls/{callId}/tracking"));
        Assert.Equal("cancelled", tracking.GetProperty("call_status").GetString());
        Assert.Equal("caller_cancelled", tracking.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task A_cancellation_request_left_unanswered_expires_when_the_run_ends()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run with { CallId = callId });
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null);
        await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "Feeling better" });
        await ProgressAsync(crew, dispatchId, "en_route_to_scene");
        await ProgressAsync(crew, dispatchId, "at_scene");

        await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/close-at-scene", new { outcome = "refused_transport" });

        Assert.Equal(CancellationRequestStatus.Expired, (await CallAsync(callId)).CancellationRequestStatus);
    }

    [Fact]
    public async Task A_patient_can_cancel_after_the_crew_declined_and_no_other_ambulance_is_on_its_way()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run with { CallId = callId });
        using var crew = await ClientAsync(run.CrewEmails[0]);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/decline", new { reason = "Flat tyre" })).StatusCode);
        await _application.WaitForDispatchProposalAsync(callId);

        var tracking = await ReadAsync(await patient.GetAsync($"/api/me/emergency-calls/{callId}/tracking"));
        Assert.True(tracking.GetProperty("looking_for_another_ambulance").GetBoolean());
        var cancelled = await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancel", new { reason = "Got a lift" });

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var call = await CallAsync(callId);
        Assert.Equal(CallStatus.Cancelled, call.Status);
        Assert.Equal(EmergencyCallOutcome.CallerCancelled, call.Outcome);
        Assert.Equal("Got a lift", call.OutcomeNotes);
    }

    [Fact]
    public async Task A_patient_reading_their_call_never_sees_crew_identity_or_run_notes()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run with { CallId = callId });
        using var crew = await ClientAsync(run.CrewEmails[0]);
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/decline", new { reason = "Driver unwell" })).StatusCode);
        await _application.WaitForDispatchProposalAsync(callId);

        var asPatient = (await ReadAsync(await patient.GetAsync($"/api/emergency-calls/{callId}"))).GetProperty("dispatches")[0];
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var asManager = (await ReadAsync(await manager.GetAsync($"/api/emergency-calls/{callId}"))).GetProperty("dispatches")[0];

        Assert.Equal("declined", asPatient.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, asPatient.GetProperty("declined_reason").ValueKind);
        Assert.Equal(0, asPatient.GetProperty("crew_staff_ids").GetArrayLength());
        Assert.Equal("Driver unwell", asManager.GetProperty("declined_reason").GetString());
        Assert.Equal(2, asManager.GetProperty("crew_staff_ids").GetArrayLength());
    }

    [Fact]
    public async Task A_patient_cannot_cancel_directly_while_an_ambulance_is_on_its_way()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        var run = await SeedRunAsync();
        await DispatchAsync(run with { CallId = callId });

        var cancelled = await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancel", new { reason = "Changed my mind" });

        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        Assert.Equal("cl_emg_010", (await ReadAsync(cancelled)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Tracking_names_the_ambulance_and_how_far_away_it_is()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreatePatientCallAsync(patient);
        var run = await SeedRunAsync(latitude: 6.95m, longitude: 79.90m);
        var dispatchId = await DispatchAsync(run with { CallId = callId });
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null);

        var tracking = await ReadAsync(await patient.GetAsync($"/api/me/emergency-calls/{callId}/tracking"));

        Assert.Equal("acknowledged", tracking.GetProperty("dispatch_status").GetString());
        Assert.True(tracking.GetProperty("ambulance_is_on_the_way").GetBoolean());
        Assert.False(tracking.GetProperty("looking_for_another_ambulance").GetBoolean());
        Assert.Equal((await AmbulanceAsync(run.AmbulanceId)).RegistrationNumber, tracking.GetProperty("ambulance_registration").GetString());
        Assert.InRange(tracking.GetProperty("ambulance_distance_km").GetDecimal(), 4m, 6m);
        Assert.Equal(JsonValueKind.Null, tracking.GetProperty("estimated_minutes_to_arrival").ValueKind);
    }

    [Fact]
    public async Task A_patient_report_fills_in_the_caller_from_their_own_record()
    {
        var kit = new NotificationTestKit(_application);
        var patient = await kit.NewPatientAsync();
        var phone = $"07{Random.Shared.NextInt64(10000000, 99999999)}";
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            await db.Patients.Where(x => x.Id == patient.PatientId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.Phone, phone));
        }

        var created = await patient.Client.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = false, latitude = 6.927079, longitude = 79.861244, location_accuracy_metres = 10,
            location_captured_at = DateTimeOffset.UtcNow, idempotency_key = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadAsync(created);
        await _application.WaitForDispatchProposalAsync(body.GetProperty("id").GetGuid());
        Assert.StartsWith("Notify Patient", body.GetProperty("caller_name").GetString());
        Assert.Equal(phone, body.GetProperty("caller_phone").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("patient_id").ValueKind);
        patient.Client.Dispose();
    }

    [Fact]
    public async Task The_waiting_clock_stops_once_a_call_is_closed()
    {
        var marker = $"wait-{Guid.NewGuid():N}";
        var callId = await SeedCallAsync(callerName: marker, createdMinutesAgo: 30);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var before = await BoardRowAsync(manager, marker);
        Assert.InRange(before.GetProperty("waiting_minutes").GetInt32(), 29, 31);

        await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/cancel", new { outcome = "false_alarm" });

        Assert.Equal(0, (await BoardRowAsync(manager, marker)).GetProperty("waiting_minutes").GetInt32());
    }

    [Fact]
    public async Task Board_search_treats_percent_and_underscore_as_plain_characters()
    {
        var marker = Guid.NewGuid().ToString("N")[..10];
        await SeedCallAsync(callerName: $"50%_off {marker}");
        await SeedCallAsync(callerName: $"50xxoff {marker}");
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var page = await manager.GetFromJsonAsync<JsonElement>(
            $"/api/emergency-calls?search={Uri.EscapeDataString($"50%_off {marker}")}");

        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal($"50%_off {marker}", row.GetProperty("caller_name").GetString());
    }

    [Fact]
    public async Task Crew_can_read_the_calls_they_were_sent_to_and_no_others()
    {
        var run = await SeedRunAsync();
        var otherCall = await SeedCallAsync();
        await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var mine = await crew.GetAsync($"/api/emergency-calls/{run.CallId}");
        var other = await crew.GetAsync($"/api/emergency-calls/{otherCall}");

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task Crew_can_read_their_own_run_and_see_where_to_go_next()
    {
        var run = await SeedRunAsync();
        var other = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        await SetAddressAsync(run.CallId, "12 Galle Road, Colombo 03");
        using var crew = await ClientAsync(run.CrewEmails[0]);
        using var outsider = await ClientAsync(other.CrewEmails[0]);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var atStart = await ReadAsync(await crew.GetAsync($"/api/dispatches/{dispatchId}"));
        Assert.Equal("12 Galle Road, Colombo 03", atStart.GetProperty("destination_label").GetString());
        Assert.Equal("Scene caller", atStart.GetProperty("caller_name").GetString());
        Assert.Equal("Chest pain", atStart.GetProperty("call_details").GetString());

        await ReachSceneAsync(crew, dispatchId);
        await ProgressAsync(crew, dispatchId, "transporting_to_hospital");
        var toHospital = await ReadAsync(await crew.GetAsync($"/api/dispatches/{dispatchId}"));
        var entrance = _application.Services.GetRequiredService<IOptions<EmergencyOptions>>().Value.HospitalEntrance.Label;
        Assert.Equal(entrance, toHospital.GetProperty("destination_label").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/dispatches/{dispatchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/dispatches/{dispatchId}")).StatusCode);
    }

    [Fact]
    public async Task Fleet_map_shows_which_ambulance_is_on_which_call()
    {
        var run = await SeedRunAsync();
        var waitingCall = await SeedCallAsync();
        var dispatchId = await DispatchAsync(run);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var map = await ReadAsync(await manager.GetAsync("/api/fleet-map"));

        var ambulance = map.GetProperty("ambulances").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == run.AmbulanceId);
        Assert.Equal(dispatchId, ambulance.GetProperty("active_dispatch_id").GetGuid());
        Assert.Equal(run.CallId, ambulance.GetProperty("active_call_id").GetGuid());
        Assert.Equal("assigned", ambulance.GetProperty("active_dispatch_status").GetString());
        Assert.False(ambulance.GetProperty("location_is_stale").GetBoolean());
        var calls = map.GetProperty("calls").EnumerateArray().ToList();
        Assert.Equal(run.AmbulanceId, calls.Single(x => x.GetProperty("id").GetGuid() == run.CallId).GetProperty("assigned_ambulance_id").GetGuid());
        Assert.Equal(JsonValueKind.Null, calls.Single(x => x.GetProperty("id").GetGuid() == waitingCall).GetProperty("assigned_ambulance_id").ValueKind);
    }

    [Fact]
    public async Task Fleet_map_leaves_out_closed_calls_and_retired_ambulances_and_is_for_duty_managers_only()
    {
        var run = await SeedRunAsync();
        var closedCall = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        await manager.PostAsJsonAsync($"/api/emergency-calls/{closedCall}/cancel", new { outcome = "false_alarm" });
        await manager.PostAsJsonAsync($"/api/ambulances/{run.AmbulanceId}/retire", new { reason = "Engine failure" });
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var map = await ReadAsync(await manager.GetAsync("/api/fleet-map"));

        Assert.DoesNotContain(map.GetProperty("calls").EnumerateArray(), x => x.GetProperty("id").GetGuid() == closedCall);
        Assert.DoesNotContain(map.GetProperty("ambulances").EnumerateArray(), x => x.GetProperty("id").GetGuid() == run.AmbulanceId);
        Assert.Equal(HttpStatusCode.Forbidden, (await crew.GetAsync("/api/fleet-map")).StatusCode);
    }

    [Fact]
    public async Task Address_search_returns_matches_and_reports_an_outage_honestly()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var run = await SeedRunAsync();
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var found = await manager.GetFromJsonAsync<JsonElement>("/api/emergency-calls/address-search?query=Ward%20Place");
        var tooShort = await manager.GetAsync("/api/emergency-calls/address-search?query=ab");
        var offline = await manager.GetAsync($"/api/emergency-calls/address-search?query={FakeAddressSearch.OfflineQuery}");
        var forbidden = await crew.GetAsync("/api/emergency-calls/address-search?query=Ward%20Place");

        var match = Assert.Single(found.EnumerateArray());
        Assert.Equal("Ward Place, Colombo", match.GetProperty("label").GetString());
        Assert.Equal(40, match.GetProperty("approximate_accuracy_metres").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, offline.StatusCode);
        Assert.Equal("cl_emg_018", (await ReadAsync(offline)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private sealed record Run(Guid CallId, Guid AmbulanceId, string[] CrewEmails);

    private async Task<Guid> DispatchAsync(Run run)
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var response = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/dispatch", new { ambulance_id = run.AmbulanceId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task ReachSceneAsync(HttpClient crew, Guid dispatchId)
    {
        Assert.Equal(HttpStatusCode.OK, (await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null)).StatusCode);
        await ProgressAsync(crew, dispatchId, "en_route_to_scene");
        await ProgressAsync(crew, dispatchId, "at_scene");
    }

    private static async Task ProgressAsync(HttpClient crew, Guid dispatchId, string status)
    {
        var response = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Run> SeedRunAsync(decimal latitude = 6.927079m, decimal longitude = 79.861244m)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var managerId = await db.StaffMembers.Where(x => x.Email == ApiApplication.ManagerEmail).Select(x => x.Id).SingleAsync();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true,
            Status = AmbulanceStatus.Available,
            CurrentLatitude = latitude,
            CurrentLongitude = longitude,
            LocationUpdatedAt = DateTimeOffset.UtcNow
        };
        db.Ambulances.Add(ambulance);
        var emails = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            var staff = new StaffMember
            {
                Id = Guid.NewGuid(),
                Email = $"closing-crew-{Guid.NewGuid():N}@carelanka.invalid",
                PasswordHash = passwords.Hash(ApiApplication.Password),
                FirstName = "Closing",
                LastName = "Crew",
                Role = StaffRole.AmbulanceCrew,
                IsActive = true
            };
            db.StaffMembers.Add(staff);
            db.AmbulanceCrewAssignments.Add(new AmbulanceCrewAssignment
            {
                Id = Guid.NewGuid(),
                AmbulanceId = ambulance.Id,
                StaffMemberId = staff.Id,
                AssignedByStaffId = managerId,
                AssignedAt = DateTimeOffset.UtcNow
            });
            emails.Add(staff.Email);
        }

        await db.SaveChangesAsync();
        return new Run(await SeedCallAsync(), ambulance.Id, emails.ToArray());
    }

    private async Task<Guid> SeedCallAsync(string callerName = "Scene caller", int createdMinutesAgo = 0)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            CallerName = callerName,
            Details = "Chest pain",
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Priority = CallPriority.High,
            Status = CallStatus.Received
        };
        db.EmergencyCalls.Add(call);
        await db.SaveChangesAsync();
        if (createdMinutesAgo > 0)
        {
            var createdAt = DateTimeOffset.UtcNow.AddMinutes(-createdMinutesAgo);
            await db.EmergencyCalls.Where(x => x.Id == call.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.CreatedAt, createdAt));
        }

        return call.Id;
    }

    private async Task SetAddressAsync(Guid callId, string label)
    {
        using var scope = _application.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().EmergencyCalls
            .Where(x => x.Id == callId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.AddressLabel, label));
    }

    private static async Task<JsonElement> BoardRowAsync(HttpClient manager, string marker)
    {
        var page = await manager.GetFromJsonAsync<JsonElement>($"/api/emergency-calls?search={marker}");
        return Assert.Single(page.GetProperty("items").EnumerateArray());
    }

    private async Task<HttpClient> PatientClientAsync()
    {
        var client = _application.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"closing-{Guid.NewGuid():N}",
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("access_token").GetString());
        return client;
    }

    private async Task<Guid> CreatePatientCallAsync(HttpClient patient)
    {
        using var response = await patient.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = false, latitude = 6.927079, longitude = 79.861244, location_accuracy_metres = 10,
            location_captured_at = DateTimeOffset.UtcNow, idempotency_key = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var callId = (await ReadAsync(response)).GetProperty("id").GetGuid();
        await _application.WaitForDispatchProposalAsync(callId);
        return callId;
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiApplication.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadAsync(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<Dispatch> LoadDispatchAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Dispatches.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<EmergencyCall> CallAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .EmergencyCalls.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<Ambulance> AmbulanceAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Ambulances.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    // Other tests' notices share the queue; answering "unavailable" leaves them to retry untouched.
    private sealed class RecordingGateway : IPreAdmissionGateway
    {
        public List<PreAdmissionRequest> Sent { get; } = [];

        public Task<PreAdmissionOutcome> SendAsync(PreAdmissionRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(PreAdmissionOutcome.Unavailable);
        }
    }
}
