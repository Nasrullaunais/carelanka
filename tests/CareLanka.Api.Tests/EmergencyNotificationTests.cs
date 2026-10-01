using System.Net;
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
public sealed class EmergencyNotificationTests
{
    private readonly ApiApplication _application;
    private readonly NotificationTestKit _kit;

    public EmergencyNotificationTests(ApiApplication application)
    {
        _application = application;
        _kit = new NotificationTestKit(application);
    }

    private sealed record Ambulance(Guid Id, (Guid Id, string Email)[] Crew);

    [Fact]
    public async Task A_new_emergency_call_alerts_every_duty_manager_except_the_one_who_took_it()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var managerId = await _kit.StaffIdAsync(ApiApplication.ManagerEmail);
        await _kit.SeedStaffAsync(StaffRole.DutyManager);

        var response = await manager.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = false,
            caller_name = "Notification test",
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 10,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await _kit.AssertSentAsync(NotificationType.EmergencyCallReceived, await IdAsync(response),
            staff: await _kit.RoleAsync(StaffRole.DutyManager, managerId), patientAccounts: [],
            actorStaffId: managerId);
    }

    [Fact]
    public async Task Dispatching_an_ambulance_tells_each_crew_member()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var ambulance = await SeedAmbulanceAsync();
        var callId = await SeedCallAsync(patientId: null);

        var dispatch = await DispatchAsync(manager, callId, ambulance);

        await _kit.AssertSentAsync(NotificationType.DispatchAssigned, dispatch,
            staff: ambulance.Crew.Select(member => member.Id), patientAccounts: [],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ManagerEmail));
    }

    [Fact]
    public async Task The_crew_setting_off_tells_the_patient_the_ambulance_is_on_the_way()
    {
        var (patient, dispatchId, crew) = await AcknowledgedRunAsync();

        var moved = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "en_route_to_scene" });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        await _kit.AssertSentAsync(NotificationType.AmbulanceOnTheWay, dispatchId,
            staff: [], patientAccounts: [patient.AccountId]);
        crew.Dispose();
    }

    [Fact]
    public async Task The_crew_reaching_the_scene_tells_the_patient_the_ambulance_has_arrived()
    {
        var (patient, dispatchId, crew) = await AcknowledgedRunAsync();
        await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "en_route_to_scene" });

        var moved = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatchId}/status", new { status = "at_scene" });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        await _kit.AssertSentAsync(NotificationType.AmbulanceArrived, dispatchId,
            staff: [], patientAccounts: [patient.AccountId]);
        crew.Dispose();
    }

    [Fact]
    public async Task Asking_to_cancel_a_dispatched_call_alerts_every_duty_manager()
    {
        var (patient, callId) = await DispatchedPatientCallAsync();

        var requested = await patient.Client.PostAsJsonAsync(
            $"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "No longer needed" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);

        await _kit.AssertSentAsync(NotificationType.CancellationRequestWaiting, callId,
            staff: await _kit.RoleAsync(StaffRole.DutyManager), patientAccounts: [],
            actorPatientAccountId: patient.AccountId);
    }

    [Fact]
    public async Task Approving_a_cancellation_tells_the_patient()
    {
        var (patient, callId) = await DispatchedPatientCallAsync();
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        await patient.Client.PostAsJsonAsync(
            $"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "No longer needed" });

        var approved = await manager.PostAsJsonAsync(
            $"/api/emergency-calls/{callId}/cancellation-request/approve", new { notes = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        await _kit.AssertSentAsync(NotificationType.CancellationAnswered, callId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ManagerEmail));
    }

    [Fact]
    public async Task Rejecting_a_cancellation_tells_the_patient()
    {
        var (patient, callId) = await DispatchedPatientCallAsync();
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        await patient.Client.PostAsJsonAsync(
            $"/api/me/emergency-calls/{callId}/cancellation-request", new { reason = "No longer needed" });

        var rejected = await manager.PostAsJsonAsync(
            $"/api/emergency-calls/{callId}/cancellation-request/reject", new { notes = "Crew is nearly there" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        await _kit.AssertSentAsync(NotificationType.CancellationAnswered, callId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ManagerEmail));
    }

    [Fact]
    public async Task A_diversion_waiting_for_approval_alerts_the_other_duty_managers_but_not_the_one_who_asked()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var managerId = await _kit.StaffIdAsync(ApiApplication.ManagerEmail);
        await _kit.SeedStaffAsync(StaffRole.DutyManager);
        var others = await OtherAmbulanceIdsAsync();
        var ambulance = await SeedAmbulanceAsync();
        await DispatchAsync(manager, await SeedCallAsync(patientId: null, CallPriority.Low), ambulance);
        var callId = await SeedCallAsync(patientId: null, CallPriority.Critical);

        var created = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
        {
            emergency_call_id = callId,
            allow_diversion = true,
            exclude_ambulance_ids = others
        });
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var proposalId = await IdAsync(created);
        await WaitUntilSettledAsync(manager, proposalId);

        await _kit.AssertSentAsync(NotificationType.DispatchProposalWaiting, callId,
            staff: await _kit.RoleAsync(StaffRole.DutyManager, managerId), patientAccounts: [],
            actorStaffId: managerId);
    }

    private async Task<(NotificationTestKit.LinkedPatient Patient, Guid DispatchId, HttpClient Crew)> AcknowledgedRunAsync()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var patient = await _kit.NewPatientAsync();
        var ambulance = await SeedAmbulanceAsync();
        var callId = await SeedCallAsync(patient.PatientId);
        var dispatchId = await DispatchAsync(manager, callId, ambulance);

        var crew = await _kit.StaffAsync(ambulance.Crew[0].Email);
        var acknowledged = await crew.PostAsync($"/api/me/dispatches/{dispatchId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);

        return (patient, dispatchId, crew);
    }

    private async Task<(NotificationTestKit.LinkedPatient Patient, Guid CallId)> DispatchedPatientCallAsync()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var patient = await _kit.NewPatientAsync();

        var created = await patient.Client.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = true,
            latitude = 6.927079,
            longitude = 79.861244,
            location_accuracy_metres = 10,
            location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var callId = await IdAsync(created);
        await _application.WaitForDispatchProposalAsync(callId);

        await DispatchAsync(manager, callId, await SeedAmbulanceAsync());

        return (patient, callId);
    }

    private static async Task<Guid> DispatchAsync(HttpClient manager, Guid callId, Ambulance ambulance)
    {
        var dispatched = await manager.PostAsJsonAsync(
            $"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulance.Id });
        Assert.Equal(HttpStatusCode.Created, dispatched.StatusCode);

        return await IdAsync(dispatched);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<List<Guid>> OtherAmbulanceIdsAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.Ambulances.AsNoTracking().Select(x => x.Id).ToListAsync();
    }

    private static async Task WaitUntilSettledAsync(HttpClient manager, Guid proposalId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            using var body = JsonDocument.Parse(
                await manager.GetStringAsync($"/api/dispatch-proposals/{proposalId}"));

            if (body.RootElement.GetProperty("status").GetString() != "pending")
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, "The dispatch proposal never left pending.");
            await Task.Delay(50);
        }
    }

    private async Task<Ambulance> SeedAmbulanceAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var managerId = await _kit.StaffIdAsync(ApiApplication.ManagerEmail);

        var ambulance = new CareLanka.Api.Data.Entities.Emergency.Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true,
            Status = AmbulanceStatus.Available,
            CurrentLatitude = 6.927079m,
            CurrentLongitude = 79.861244m,
            LocationUpdatedAt = DateTimeOffset.UtcNow
        };
        db.Ambulances.Add(ambulance);

        var crew = new List<(Guid, string)>();
        for (var index = 0; index < 2; index++)
        {
            var staff = new StaffMember
            {
                Id = Guid.NewGuid(),
                Email = $"notify-crew-{Guid.NewGuid():N}@carelanka.invalid",
                PasswordHash = passwords.Hash(ApiApplication.Password),
                FirstName = "Notify",
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
            crew.Add((staff.Id, staff.Email));
        }

        await db.SaveChangesAsync();
        return new Ambulance(ambulance.Id, crew.ToArray());
    }

    private async Task<Guid> SeedCallAsync(Guid? patientId, CallPriority priority = CallPriority.High)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Priority = priority,
            Status = CallStatus.Received
        };
        db.EmergencyCalls.Add(call);
        await db.SaveChangesAsync();
        return call.Id;
    }
}
