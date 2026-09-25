using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Data.Entities.Common;
using Microsoft.Extensions.Logging.Abstractions;
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
public sealed class DispatchProposalEndpointTests
{
    private readonly ApiApplication _application;

    public DispatchProposalEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_free_ambulance_is_proposed_and_confirming_it_creates_a_dispatch()
    {
        var others = await OtherAmbulanceIdsAsync();
        var ambulanceId = await SeedReadyAmbulanceAsync();
        var callId = await SeedCallAsync(CallPriority.High);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var proposal = await SettledAsync(manager, await StartAsync(manager, callId, others));

        Assert.Equal("pending_confirmation", proposal.GetProperty("status").GetString());
        Assert.Equal("free_ambulance_proposed", proposal.GetProperty("outcome").GetString());
        Assert.False(proposal.GetProperty("is_diversion").GetBoolean());
        var proposalId = proposal.GetProperty("id").GetGuid();

        var confirm = await manager.PostAsync($"/api/dispatch-proposals/{proposalId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmed = await ReadAsync(confirm);
        Assert.Equal("executed", confirmed.GetProperty("status").GetString());
        var dispatchId = confirmed.GetProperty("resulting_dispatch_id").GetGuid();

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var dispatch = await db.Dispatches.AsNoTracking().SingleAsync(x => x.Id == dispatchId);
        Assert.Equal(ambulanceId, dispatch.AmbulanceId);
        Assert.Equal(proposalId, dispatch.DispatchProposalId);
    }

    [Fact]
    public async Task Nothing_free_and_diversion_disallowed_fails_honestly()
    {
        var others = await OtherAmbulanceIdsAsync();
        var callId = await SeedCallAsync(CallPriority.Critical);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
        {
            emergency_call_id = callId, allow_diversion = false, exclude_ambulance_ids = others
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var proposalId = (await ReadAsync(create)).GetProperty("id").GetGuid();

        var proposal = await SettledAsync(manager, proposalId);
        Assert.Equal("failed", proposal.GetProperty("status").GetString());
        Assert.Equal("no_ambulance_available", proposal.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Only_a_duty_manager_can_ask_for_a_proposal()
    {
        var callId = await SeedCallAsync(CallPriority.High);
        using var crew = await ClientAsync(await SeedCrewEmailAsync());

        var response = await crew.PostAsJsonAsync("/api/dispatch-proposals", new { emergency_call_id = callId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Confirming_a_stale_proposal_is_refused_and_names_the_failed_check()
    {
        var others = await OtherAmbulanceIdsAsync();
        var ambulanceId = await SeedReadyAmbulanceAsync();
        var callId = await SeedCallAsync(CallPriority.High);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var proposal = await SettledAsync(manager, await StartAsync(manager, callId, others));
        var proposalId = proposal.GetProperty("id").GetGuid();

        var otherCall = await SeedCallAsync(CallPriority.High);
        var stolen = await manager.PostAsJsonAsync($"/api/emergency-calls/{otherCall}/dispatch", new { ambulance_id = ambulanceId });
        Assert.Equal(HttpStatusCode.Created, stolen.StatusCode);

        var confirm = await manager.PostAsync($"/api/dispatch-proposals/{proposalId}/confirm", null);

        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        var body = await ReadAsync(confirm);
        Assert.Contains("ambulance_still_eligible", body.GetProperty("failed_checks").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Rejecting_a_proposal_leaves_the_call_unassigned()
    {
        var others = await OtherAmbulanceIdsAsync();
        var ambulanceId = await SeedReadyAmbulanceAsync();
        var callId = await SeedCallAsync(CallPriority.High);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var proposal = await SettledAsync(manager, await StartAsync(manager, callId, others));
        var proposalId = proposal.GetProperty("id").GetGuid();
        _ = ambulanceId;

        var reject = await manager.PostAsJsonAsync($"/api/dispatch-proposals/{proposalId}/reject", new { reason = "no_longer_needed" });

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var body = await ReadAsync(reject);
        Assert.Equal("rejected", body.GetProperty("status").GetString());

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = await db.EmergencyCalls.AsNoTracking().SingleAsync(x => x.Id == callId);
        Assert.Equal(CallStatus.Received, call.Status);
    }

    [Fact]
    public async Task A_call_already_dispatched_cannot_be_planned_again()
    {
        var ambulanceId = await SeedReadyAmbulanceAsync();
        var callId = await SeedCallAsync(CallPriority.High);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulanceId });
        Assert.Equal(HttpStatusCode.Created, dispatched.StatusCode);

        var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new { emergency_call_id = callId });

        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        Assert.Equal("cl_emg_007", (await ReadAsync(create)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("patient_reached")]
    [InlineData("priority_changed")]
    [InlineData("crew_unavailable")]
    public async Task Diversion_rechecks_live_state_before_moving_an_ambulance(string? change)
    {
        var others = await OtherAmbulanceIdsAsync();
        var ambulanceId = await SeedReadyAmbulanceAsync();
        var sourceCallId = await SeedCallAsync(CallPriority.Low);
        var urgentCallId = await SeedCallAsync(CallPriority.Critical);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var assigned = await manager.PostAsJsonAsync($"/api/emergency-calls/{sourceCallId}/dispatch", new { ambulance_id = ambulanceId });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
        {
            emergency_call_id = urgentCallId, allow_diversion = true, exclude_ambulance_ids = others
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var proposal = await SettledAsync(manager, (await ReadAsync(create)).GetProperty("id").GetGuid());
        Assert.Equal("pending_approval", proposal.GetProperty("status").GetString());
        if (change is not null)
        {
            using var changes = _application.Services.CreateScope();
            var state = changes.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            if (change == "patient_reached") (await state.Dispatches.SingleAsync(x => x.EmergencyCallId == sourceCallId)).Status = DispatchStatus.AtScene;
            if (change == "priority_changed") (await state.EmergencyCalls.FindAsync(urgentCallId))!.Priority = CallPriority.Low;
            if (change == "crew_unavailable")
                foreach (var assignment in await state.AmbulanceCrewAssignments.Where(x => x.AmbulanceId == ambulanceId).ToListAsync()) assignment.UnassignedAt = DateTimeOffset.UtcNow;
            await state.SaveChangesAsync();
        }
        var approved = await manager.PostAsJsonAsync($"/api/dispatch-proposals/{proposal.GetProperty("id").GetGuid()}/approve", new { notes = "Urgent response required" });
        if (change is not null)
        {
            Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
            using var check = _application.Services.CreateScope();
            var unchanged = check.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            Assert.Equal(CallStatus.Received, (await unchanged.EmergencyCalls.FindAsync(urgentCallId))!.Status);
            Assert.False(await unchanged.Dispatches.AnyAsync(x => x.EmergencyCallId == urgentCallId));
            return;
        }
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.Equal(CallStatus.Received, (await db.EmergencyCalls.FindAsync(sourceCallId))!.Status);
        Assert.Equal(CallStatus.Dispatched, (await db.EmergencyCalls.FindAsync(urgentCallId))!.Status);
        Assert.Equal(DispatchStatus.Reassigned, (await db.Dispatches.SingleAsync(x => x.EmergencyCallId == sourceCallId)).Status);
        Assert.Equal(ambulanceId, (await db.Dispatches.SingleAsync(x => x.EmergencyCallId == urgentCallId)).AmbulanceId);
    }

    [Fact]
    public async Task Pending_proposals_are_recovered_without_an_in_memory_queue_entry()
    {
        var callId = await SeedCallAsync(CallPriority.High);
        var proposalId = Guid.NewGuid();
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var workflow = new AgentWorkflow
            {
                Id = Guid.NewGuid(), AgentType = AgentType.DispatchRouting, EntityType = "EmergencyCall",
                EntityId = callId, CorrelationId = Guid.NewGuid(), Objective = "Recovery test",
                Status = AgentWorkflowStatus.Pending, StartedAt = DateTimeOffset.UtcNow, AttemptCount = 1
            };
            db.AgentWorkflows.Add(workflow);
            db.DispatchProposals.Add(new DispatchProposal
            {
                Id = proposalId, WorkflowId = workflow.Id, EmergencyCallId = callId,
                CallPriority = CallPriority.High, Status = DispatchProposalStatus.Pending
            });
            await db.SaveChangesAsync();
        }
        using var worker = new DispatchProposalWorker(new DispatchRunQueue(), _application.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<DispatchProposalWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var manager = await ClientAsync(ApiApplication.ManagerEmail);
            var result = await SettledAsync(manager, proposalId);
            Assert.NotEqual("pending", result.GetProperty("status").GetString());
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private async Task<Guid> StartAsync(HttpClient manager, Guid callId, IReadOnlyList<Guid>? excludeAmbulanceIds = null)
    {
        var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
        {
            emergency_call_id = callId, exclude_ambulance_ids = excludeAmbulanceIds ?? []
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        return (await ReadAsync(create)).GetProperty("id").GetGuid();
    }

    private async Task<List<Guid>> OtherAmbulanceIdsAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.Ambulances.AsNoTracking().Select(x => x.Id).ToListAsync();
    }

    private static async Task<JsonElement> SettledAsync(HttpClient client, Guid proposalId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            var response = await client.GetAsync($"/api/dispatch-proposals/{proposalId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await ReadAsync(response);
            var status = body.GetProperty("status").GetString();

            if (status != "pending")
            {
                return body;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Dispatch proposal never left pending.");
            }

            await Task.Delay(50);
        }
    }

    private async Task<Guid> SeedReadyAmbulanceAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var managerId = await db.StaffMembers.Where(x => x.Email == ApiApplication.ManagerEmail).Select(x => x.Id).SingleAsync();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true,
            Status = AmbulanceStatus.Available,
            CurrentLatitude = 6.927079m,
            CurrentLongitude = 79.861244m,
            LocationUpdatedAt = DateTimeOffset.UtcNow
        };
        db.Ambulances.Add(ambulance);
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        for (var index = 0; index < 2; index++)
        {
            var staff = new StaffMember
            {
                Id = Guid.NewGuid(),
                Email = $"proposal-crew-{Guid.NewGuid():N}@carelanka.invalid",
                PasswordHash = passwords.Hash(ApiApplication.Password),
                FirstName = "Proposal",
                LastName = "Crew",
                Role = StaffRole.AmbulanceCrew,
                IsActive = true
            };
            db.StaffMembers.Add(staff);
            db.AmbulanceCrewAssignments.Add(new AmbulanceCrewAssignment
            {
                Id = Guid.NewGuid(), AmbulanceId = ambulance.Id, StaffMemberId = staff.Id,
                AssignedByStaffId = managerId, AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return ambulance.Id;
    }

    private async Task<string> SeedCrewEmailAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(),
            Email = $"proposal-outsider-{Guid.NewGuid():N}@carelanka.invalid",
            PasswordHash = passwords.Hash(ApiApplication.Password),
            FirstName = "Outsider",
            LastName = "Crew",
            Role = StaffRole.AmbulanceCrew,
            IsActive = true
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return staff.Email;
    }

    private async Task<Guid> SeedCallAsync(CallPriority priority)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Priority = priority,
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
}
