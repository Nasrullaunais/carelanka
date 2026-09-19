using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DispatchEndpointTests
{
    private readonly ApiApplication _application;

    public DispatchEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Manual_dispatch_snapshots_the_current_crew_and_marks_the_call_dispatched()
    {
        var run = await SeedRunAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var response = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/dispatch", new { ambulance_id = run.AmbulanceId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("assigned", body.GetProperty("status").GetString());
        Assert.Equal(2, body.GetProperty("crew_count").GetInt32());
        Assert.False(body.GetProperty("acknowledgement_overdue").GetBoolean());
        Assert.Equal(CallStatus.Dispatched, (await CallAsync(run.CallId)).Status);
        Assert.Equal(AmbulanceStatus.Dispatched, (await AmbulanceAsync(run.AmbulanceId)).Status);
    }

    [Fact]
    public async Task Only_a_duty_manager_can_dispatch()
    {
        var run = await SeedRunAsync();
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var response = await crew.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/dispatch", new { ambulance_id = run.AmbulanceId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_ineligible_ambulance_is_refused_with_the_reasons()
    {
        var run = await SeedRunAsync(crewCount: 1);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var response = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/dispatch", new { ambulance_id = run.AmbulanceId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("cl_emg_005", body.GetProperty("code").GetString());
        Assert.Contains("insufficient_crew", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Two_confirmations_for_one_call_produce_exactly_one_dispatch()
    {
        var first = await SeedRunAsync();
        var second = await SeedRunAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var responses = await Task.WhenAll(
            manager.PostAsJsonAsync($"/api/emergency-calls/{first.CallId}/dispatch", new { ambulance_id = first.AmbulanceId }),
            manager.PostAsJsonAsync($"/api/emergency-calls/{first.CallId}/dispatch", new { ambulance_id = second.AmbulanceId }));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("cl_emg_006", (await ReadAsync(loser)).GetProperty("code").GetString());
        Assert.Equal(1, await LiveDispatchCountForCallAsync(first.CallId));
    }

    [Fact]
    public async Task Crew_takes_a_run_from_acknowledgement_to_handover()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var active = await crew.GetFromJsonAsync<JsonElement>("/api/me/dispatches/active");
        Assert.Equal(dispatchId, active.GetProperty("id").GetGuid());

        Assert.Equal("acknowledged", await PostStatusAsync(crew, $"/api/me/dispatches/{dispatchId}/acknowledge"));
        Assert.Equal("en_route_to_scene", await ProgressAsync(crew, dispatchId, "en_route_to_scene"));
        Assert.Equal("at_scene", await ProgressAsync(crew, dispatchId, "at_scene"));
        Assert.Equal("transporting_to_hospital", await ProgressAsync(crew, dispatchId, "transporting_to_hospital"));

        var handover = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/handover", new
        {
            notes = "  Handed to triage nurse.  ",
            patient_condition = "Conscious and breathing normally"
        });

        Assert.Equal(HttpStatusCode.OK, handover.StatusCode);
        var body = await ReadAsync(handover);
        Assert.Equal("handed_over", body.GetProperty("status").GetString());
        Assert.Equal("Handed to triage nurse.", body.GetProperty("handover_notes").GetString());
        Assert.Equal("Conscious and breathing normally", body.GetProperty("patient_condition").GetString());
        Assert.Equal(CallStatus.Completed, (await CallAsync(run.CallId)).Status);
        Assert.Equal(AmbulanceStatus.Available, (await AmbulanceAsync(run.AmbulanceId)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await crew.GetAsync("/api/me/dispatches/active")).StatusCode);
    }

    [Fact]
    public async Task A_status_cannot_be_skipped()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var skipped = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "at_scene" });
        var handover = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/handover", new { });

        Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, handover.StatusCode);
    }

    [Fact]
    public async Task Crew_who_did_not_respond_cannot_touch_the_dispatch()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        var outsider = await SeedCrewAsync();
        using var client = await ClientAsync(outsider.Email);

        var response = await client.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Declining_records_the_reason_and_reopens_the_call_for_dispatch()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var declined = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/decline", new { reason = "Flat tyre" });

        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        var body = await ReadAsync(declined);
        Assert.Equal("declined", body.GetProperty("status").GetString());
        Assert.Equal("Flat tyre", body.GetProperty("declined_reason").GetString());
        Assert.Equal(CallStatus.Received, (await CallAsync(run.CallId)).Status);
        Assert.Equal(AmbulanceStatus.Available, (await AmbulanceAsync(run.AmbulanceId)).Status);

        var other = await SeedRunAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var again = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/dispatch", new { ambulance_id = other.AmbulanceId });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task One_crew_member_acknowledging_while_another_declines_leaves_one_winner()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var first = await ClientAsync(run.CrewEmails[0]);
        using var second = await ClientAsync(run.CrewEmails[1]);

        var responses = await Task.WhenAll(
            first.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null),
            second.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/decline", new { reason = "Unable to respond" }));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var dispatch = await LoadDispatchAsync(dispatchId);
        var call = await CallAsync(run.CallId);
        Assert.Equal(dispatch.Status == DispatchStatus.Declined, call.Status == CallStatus.Received);
    }

    [Fact]
    public async Task Reassigning_before_the_scene_moves_the_call_to_another_ambulance()
    {
        var run = await SeedRunAsync();
        var replacement = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var response = await manager.PostAsJsonAsync($"/api/dispatches/{dispatchId}/reassign", new
        {
            replacement_ambulance_id = replacement.AmbulanceId,
            reason = "Closer ambulance became free"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(replacement.AmbulanceId, body.GetProperty("ambulance_id").GetGuid());
        var old = await LoadDispatchAsync(dispatchId);
        Assert.Equal(DispatchStatus.Reassigned, old.Status);
        Assert.Equal("Closer ambulance became free", old.ReassignmentReason);
        Assert.Equal(body.GetProperty("id").GetGuid(), old.SupersededByDispatchId);
        Assert.Equal(AmbulanceStatus.Available, (await AmbulanceAsync(run.AmbulanceId)).Status);
        Assert.Equal(CallStatus.Dispatched, (await CallAsync(run.CallId)).Status);
    }

    [Fact]
    public async Task Cancelling_before_the_scene_records_the_reason_and_frees_the_ambulance()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var response = await manager.PostAsJsonAsync($"/api/dispatches/{dispatchId}/cancel", new { reason = "Caller reached a clinic" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("cancelled", body.GetProperty("status").GetString());
        Assert.Equal("Caller reached a clinic", body.GetProperty("cancellation_reason").GetString());
        Assert.Equal(AmbulanceStatus.Available, (await AmbulanceAsync(run.AmbulanceId)).Status);
    }

    [Fact]
    public async Task History_lists_only_my_finished_runs_newest_first()
    {
        var first = await SeedRunAsync();
        var second = await SeedRunAsync();
        var firstDispatch = await DispatchAsync(first);
        var secondDispatch = await DispatchAsync(second);
        using var crew = await ClientAsync(first.CrewEmails[0]);
        using var otherCrew = await ClientAsync(second.CrewEmails[0]);
        Assert.Equal(0, (await crew.GetFromJsonAsync<JsonElement>("/api/me/dispatches/history")).GetProperty("total_items").GetInt32());

        await crew.PostAsJsonAsync($"/api/me/dispatches/{firstDispatch}/decline", new { reason = "Flat tyre" });
        await otherCrew.PostAsJsonAsync($"/api/me/dispatches/{secondDispatch}/decline", new { reason = "Sick" });

        var mine = await crew.GetFromJsonAsync<JsonElement>("/api/me/dispatches/history");
        Assert.Equal(1, mine.GetProperty("total_items").GetInt32());
        Assert.Equal(firstDispatch, mine.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal("declined", mine.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task History_filters_by_date_and_rejects_a_backwards_range()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/decline", new { reason = "Flat tyre" });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inRange = await crew.GetFromJsonAsync<JsonElement>($"/api/me/dispatches/history?from={today:O}&to={today:O}");
        var past = await crew.GetFromJsonAsync<JsonElement>($"/api/me/dispatches/history?to={today.AddDays(-2):O}");
        var backwards = await crew.GetAsync($"/api/me/dispatches/history?from={today:O}&to={today.AddDays(-1):O}");

        Assert.Equal(1, inRange.GetProperty("total_items").GetInt32());
        Assert.Equal(0, past.GetProperty("total_items").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }

    [Fact]
    public async Task Navigation_points_to_the_scene_then_the_hospital_and_only_for_the_assigned_crew()
    {
        var run = await SeedRunAsync();
        var outsider = await SeedCrewAsync();
        var dispatchId = await DispatchAsync(run);
        var url = $"/api/me/dispatches/{dispatchId}/navigation";
        using var crew = await ClientAsync(run.CrewEmails[0]);
        using var other = await ClientAsync(outsider.Email);
        await PostStatusAsync(crew, $"/api/me/dispatches/{dispatchId}/acknowledge");
        await ProgressAsync(crew, dispatchId, "en_route_to_scene");

        var toScene = await crew.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("scene", toScene.GetProperty("waypoint_type").GetString());
        Assert.Equal(6.9271, toScene.GetProperty("destination_latitude").GetDouble(), 4);
        Assert.Contains("destination=6.9271,79.8612", toScene.GetProperty("google_maps_url").GetString());

        await ProgressAsync(crew, dispatchId, "at_scene");
        await ProgressAsync(crew, dispatchId, "transporting_to_hospital");
        var toHospital = await crew.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("hospital_emergency_entrance", toHospital.GetProperty("waypoint_type").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(url)).StatusCode);

        await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/handover", new { });
        Assert.Equal(HttpStatusCode.Conflict, (await crew.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Nothing_can_divert_a_crew_that_has_reached_the_scene()
    {
        var run = await SeedRunAsync();
        var replacement = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);
        await PostStatusAsync(crew, $"/api/me/dispatches/{dispatchId}/acknowledge");
        await ProgressAsync(crew, dispatchId, "en_route_to_scene");
        await ProgressAsync(crew, dispatchId, "at_scene");
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var cancel = await manager.PostAsJsonAsync($"/api/dispatches/{dispatchId}/cancel", new { reason = "Changed mind" });
        var reassign = await manager.PostAsJsonAsync($"/api/dispatches/{dispatchId}/reassign", new
        {
            replacement_ambulance_id = replacement.AmbulanceId,
            reason = "Closer ambulance"
        });

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reassign.StatusCode);
        Assert.Equal(DispatchStatus.AtScene, (await LoadDispatchAsync(dispatchId)).Status);
    }

    [Fact]
    public async Task An_unacknowledged_dispatch_is_flagged_overdue_after_the_timeout()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var fresh = await manager.GetFromJsonAsync<JsonElement>($"/api/emergency-calls/{run.CallId}");
        Assert.False(fresh.GetProperty("dispatches")[0].GetProperty("acknowledgement_overdue").GetBoolean());

        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            await db.Dispatches.Where(x => x.Id == dispatchId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.DispatchedAt, DateTimeOffset.UtcNow.AddMinutes(-2)));
        }

        var stale = await manager.GetFromJsonAsync<JsonElement>($"/api/emergency-calls/{run.CallId}");
        Assert.True(stale.GetProperty("dispatches")[0].GetProperty("acknowledgement_overdue").GetBoolean());
    }

    [Fact]
    public async Task Handover_details_are_length_limited()
    {
        var run = await SeedRunAsync();
        var dispatchId = await DispatchAsync(run);
        using var crew = await ClientAsync(run.CrewEmails[0]);

        var response = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/handover", new
        {
            patient_condition = new string('x', 501)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record Run(Guid CallId, Guid AmbulanceId, string[] CrewEmails);

    private sealed record CrewLogin(Guid Id, string Email);

    private async Task<Guid> DispatchAsync(Run run)
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var response = await manager.PostAsJsonAsync($"/api/emergency-calls/{run.CallId}/dispatch", new { ambulance_id = run.AmbulanceId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Run> SeedRunAsync(int crewCount = 2)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var managerId = await db.StaffMembers.Where(x => x.Email == ApiApplication.ManagerEmail).Select(x => x.Id).SingleAsync();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = $"D{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true,
            Status = AmbulanceStatus.Available,
            CurrentLatitude = 6.927079m,
            CurrentLongitude = 79.861244m,
            LocationUpdatedAt = DateTimeOffset.UtcNow
        };
        db.Ambulances.Add(ambulance);
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var crew = new List<CrewLogin>();
        for (var index = 0; index < crewCount; index++)
        {
            var member = AddCrew(db, passwords);
            crew.Add(member);
            db.AmbulanceCrewAssignments.Add(new AmbulanceCrewAssignment
            {
                Id = Guid.NewGuid(),
                AmbulanceId = ambulance.Id,
                StaffMemberId = member.Id,
                AssignedByStaffId = managerId,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return new Run(await SeedCallAsync(), ambulance.Id, crew.Select(x => x.Email).ToArray());
    }

    private async Task<CrewLogin> SeedCrewAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var member = AddCrew(db, scope.ServiceProvider.GetRequiredService<IPasswordService>());
        await db.SaveChangesAsync();
        return member;
    }

    private static CrewLogin AddCrew(CareLankaDbContext db, IPasswordService passwords)
    {
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(),
            Email = $"crew-{Guid.NewGuid():N}@carelanka.invalid",
            PasswordHash = passwords.Hash(ApiApplication.Password),
            FirstName = "Dispatch",
            LastName = "Crew",
            Role = StaffRole.AmbulanceCrew,
            IsActive = true
        };
        db.StaffMembers.Add(staff);
        return new CrewLogin(staff.Id, staff.Email);
    }

    private async Task<Guid> SeedCallAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Priority = CallPriority.High,
            Status = CallStatus.Received
        };
        db.EmergencyCalls.Add(call);
        await db.SaveChangesAsync();
        return call.Id;
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiApplication.Password });
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

    private static async Task<string?> PostStatusAsync(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("status").GetString();
    }

    private static async Task<string?> ProgressAsync(HttpClient client, Guid dispatchId, string status)
    {
        var response = await client.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("status").GetString();
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
            .Ambulances.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<int> LiveDispatchCountForCallAsync(Guid callId)
    {
        using var scope = _application.Services.CreateScope();
        var live = new[]
        {
            DispatchStatus.Assigned, DispatchStatus.Acknowledged, DispatchStatus.EnRouteToScene,
            DispatchStatus.AtScene, DispatchStatus.TransportingToHospital
        };
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Dispatches.CountAsync(x => x.EmergencyCallId == callId && live.Contains(x.Status));
    }
}
