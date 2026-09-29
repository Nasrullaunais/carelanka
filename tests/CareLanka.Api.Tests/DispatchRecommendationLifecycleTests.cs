using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DispatchRecommendationLifecycleTests
{
    private readonly ApiApplication _application;

    public DispatchRecommendationLifecycleTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_new_call_already_has_a_recommendation_on_the_board_and_in_detail()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var details = $"board-{Guid.NewGuid():N}";
        var callId = await CreateCallAsync(manager, Guid.NewGuid(), details);

        var detail = await ReadAsync(await manager.GetAsync($"/api/emergency-calls/{callId}"));
        var latest = detail.GetProperty("latest_proposal");
        Assert.Equal(callId, latest.GetProperty("emergency_call_id").GetGuid());

        var settled = await SettledAsync(manager, latest.GetProperty("id").GetGuid());
        Assert.NotEqual("pending", settled.GetProperty("status").GetString());

        var board = await ReadAsync(await manager.GetAsync($"/api/emergency-calls?search={details}"));
        var row = Assert.Single(board.GetProperty("items").EnumerateArray());
        Assert.Equal(settled.GetProperty("id").GetGuid(), row.GetProperty("latest_proposal").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_patient_call_gets_a_recommendation_too()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreateCallAsync(patient, Guid.NewGuid());

        Assert.Equal(1, await ProposalCountAsync(callId));
    }

    [Fact]
    public async Task Sending_the_same_call_twice_opens_one_recommendation()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var key = Guid.NewGuid();

        var first = await CreateCallAsync(manager, key);
        var second = await CreateCallAsync(manager, key);

        Assert.Equal(first, second);
        Assert.Equal(1, await ProposalCountAsync(first));
    }

    [Fact]
    public async Task Dispatching_by_hand_withdraws_the_open_recommendation()
    {
        var ambulance = await SeedAmbulanceAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreateCallAsync(manager, Guid.NewGuid());

        var dispatched = await manager.PostAsJsonAsync($"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulance.Id });

        Assert.Equal(HttpStatusCode.Created, dispatched.StatusCode);
        var proposal = await OnlyProposalAsync(callId);
        Assert.Equal(DispatchProposalStatus.Withdrawn, proposal.Status);
        Assert.Equal(DispatchWithdrawalReason.DispatchedManually, proposal.WithdrawalReason);
        Assert.Null(proposal.ReviewedByStaffMemberId);
        Assert.Equal(AgentWorkflowStatus.Withdrawn, (await WorkflowAsync(proposal.WorkflowId)).Status);
    }

    [Fact]
    public async Task A_patient_cancelling_before_dispatch_withdraws_the_recommendation()
    {
        using var patient = await PatientClientAsync();
        var callId = await CreateCallAsync(patient, Guid.NewGuid());
        await MakeReadyAsync(callId);

        var cancelled = await patient.PostAsJsonAsync($"/api/me/emergency-calls/{callId}/cancel", new { reason = "Feeling better" });

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var proposal = await OnlyProposalAsync(callId);
        Assert.Equal(DispatchProposalStatus.Withdrawn, proposal.Status);
        Assert.Equal(DispatchWithdrawalReason.CallClosed, proposal.WithdrawalReason);
    }

    [Fact]
    public async Task Changing_priority_replaces_the_recommendation_but_saving_the_same_priority_does_not()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreateCallAsync(manager, Guid.NewGuid());
        await MakeReadyAsync(callId);

        var same = await manager.PatchAsJsonAsync($"/api/emergency-calls/{callId}", new { priority = "high" });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(1, await ProposalCountAsync(callId));

        var raised = await manager.PatchAsJsonAsync($"/api/emergency-calls/{callId}", new { priority = "critical" });

        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        var proposals = await ProposalsAsync(callId);
        Assert.Equal(2, proposals.Count);
        Assert.Equal(DispatchWithdrawalReason.CallChanged, proposals[0].WithdrawalReason);
        Assert.Equal(CallPriority.Critical, proposals[1].CallPriority);
        Assert.NotEqual(DispatchProposalStatus.Withdrawn, proposals[1].Status);
    }

    [Fact]
    public async Task Rejecting_an_ambulance_as_unsuitable_asks_again_without_it()
    {
        var others = await AmbulanceIdsAsync();
        await SeedAmbulanceAsync();
        await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var proposal = await SettledAsync(manager, await StartAsync(manager, callId, others));
        Assert.Equal("pending_confirmation", proposal.GetProperty("status").GetString());
        var rejectedAmbulance = proposal.GetProperty("proposed_ambulance_id").GetGuid();

        var reject = await manager.PostAsJsonAsync(
            $"/api/dispatch-proposals/{proposal.GetProperty("id").GetGuid()}/reject", new { reason = "ambulance_unsuitable" });

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var followUp = (await ProposalsAsync(callId)).Last();
        Assert.NotEqual(proposal.GetProperty("id").GetGuid(), followUp.Id);
        var excluded = JsonSerializer.Deserialize<List<Guid>>(followUp.ExcludeAmbulanceIdsJson!)!;
        Assert.Contains(rejectedAmbulance, excluded);
        Assert.True(others.All(excluded.Contains));
        var settled = await SettledAsync(manager, followUp.Id);
        Assert.NotEqual(rejectedAmbulance, settled.GetProperty("proposed_ambulance_id").GetGuid());
    }

    [Fact]
    public async Task Rejecting_an_unsafe_diversion_asks_again_with_diversion_off()
    {
        var others = await AmbulanceIdsAsync();
        var ambulance = await SeedAmbulanceAsync();
        var sourceCall = await SeedCallAsync(CallPriority.Low);
        var urgentCall = await SeedCallAsync(CallPriority.Critical);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        Assert.Equal(HttpStatusCode.Created,
            (await manager.PostAsJsonAsync($"/api/emergency-calls/{sourceCall}/dispatch", new { ambulance_id = ambulance.Id })).StatusCode);
        var proposal = await SettledAsync(manager, await StartAsync(manager, urgentCall, others));
        Assert.Equal("pending_approval", proposal.GetProperty("status").GetString());

        var reject = await manager.PostAsJsonAsync(
            $"/api/dispatch-proposals/{proposal.GetProperty("id").GetGuid()}/reject", new { reason = "unsafe_diversion", notes = "Crew nearly there" });

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var followUp = (await ProposalsAsync(urgentCall)).Last();
        Assert.False(followUp.AllowDiversion);
        Assert.Equal("failed", (await SettledAsync(manager, followUp.Id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Rejecting_as_no_longer_needed_does_not_ask_again()
    {
        var others = await AmbulanceIdsAsync();
        await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var proposal = await SettledAsync(manager, await StartAsync(manager, callId, others));

        await manager.PostAsJsonAsync(
            $"/api/dispatch-proposals/{proposal.GetProperty("id").GetGuid()}/reject", new { reason = "no_longer_needed" });

        Assert.Equal(1, await ProposalCountAsync(callId));
    }

    [Fact]
    public async Task A_crew_declining_opens_a_new_recommendation_without_their_ambulance()
    {
        var ambulance = await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var dispatch = await ReadAsync(await manager.PostAsJsonAsync(
            $"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulance.Id }));
        using var crew = await ClientAsync(ambulance.CrewEmail);

        var declined = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatch.GetProperty("id").GetGuid()}/decline", new { reason = "Flat tyre" });

        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        var reopened = Assert.Single(await ProposalsAsync(callId));
        Assert.Contains(ambulance.Id, JsonSerializer.Deserialize<List<Guid>>(reopened.ExcludeAmbulanceIdsJson!)!);
    }

    [Fact]
    public async Task Two_rechecks_at_once_give_one_recommendation_and_one_conflict()
    {
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var responses = await Task.WhenAll(
            manager.PostAsJsonAsync("/api/dispatch-proposals", new { emergency_call_id = callId }),
            manager.PostAsJsonAsync("/api/dispatch-proposals", new { emergency_call_id = callId }));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Accepted);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_agent_answer_that_lands_after_withdrawal_is_dropped()
    {
        var callId = await SeedCallAsync();
        Guid proposalId;
        using (var scope = _application.Services.CreateScope())
        {
            var lifecycle = scope.ServiceProvider.GetRequiredService<IDispatchProposalLifecycle>();
            proposalId = lifecycle.Open(callId, CallPriority.High, allowDiversion: true, []).Id;
            await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().SaveChangesAsync();
        }

        using (var scope = _application.Services.CreateScope())
        {
            var executor = new DispatchProposalExecutor(
                scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
                new WithdrawingAgent(_application.Services, callId),
                scope.ServiceProvider.GetRequiredService<INotifier>(),
                NullLogger<DispatchProposalExecutor>.Instance);
            await executor.ExecuteAsync(proposalId);
        }

        var proposal = await OnlyProposalAsync(callId);
        Assert.Equal(DispatchProposalStatus.Withdrawn, proposal.Status);
        Assert.Null(proposal.ProposedAmbulanceId);
    }

    [Fact]
    public async Task A_failed_recommendation_rings_the_duty_manager_about_the_call()
    {
        var others = await AmbulanceIdsAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
        {
            emergency_call_id = callId, allow_diversion = false, exclude_ambulance_ids = others
        });

        Assert.Equal("failed", (await SettledAsync(manager, (await ReadAsync(create)).GetProperty("id").GetGuid())).GetProperty("status").GetString());
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.True(await db.Notifications.AnyAsync(x =>
            x.Type == NotificationType.DispatchProposalFailed && x.EntityType == "emergency_call" && x.EntityId == callId));
    }

    [Fact]
    public async Task A_ready_recommendation_does_not_ring_a_second_bell()
    {
        var others = await AmbulanceIdsAsync();
        await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var proposal = await SettledAsync(manager, await StartAsync(manager, callId, others));

        Assert.Equal("pending_confirmation", proposal.GetProperty("status").GetString());
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.False(await db.Notifications.AnyAsync(x => x.EntityId == callId && x.Type != NotificationType.EmergencyCallReceived));
    }

    [Fact]
    public async Task Moving_the_scene_replaces_the_recommendation_but_saving_the_same_spot_does_not()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var callId = await CreateCallAsync(manager, Guid.NewGuid());
        await MakeReadyAsync(callId);

        var same = await manager.PatchAsJsonAsync($"/api/emergency-calls/{callId}", new { latitude = 6.927079, longitude = 79.861244 });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(1, await ProposalCountAsync(callId));

        var moved = await manager.PatchAsJsonAsync($"/api/emergency-calls/{callId}", new { latitude = 6.95, longitude = 79.88 });

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var proposals = await ProposalsAsync(callId);
        Assert.Equal(2, proposals.Count);
        Assert.Equal(DispatchWithdrawalReason.CallChanged, proposals[0].WithdrawalReason);
    }

    [Fact]
    public async Task Cancelling_a_dispatch_opens_a_new_recommendation_without_that_ambulance()
    {
        var ambulance = await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var dispatch = await ReadAsync(await manager.PostAsJsonAsync(
            $"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulance.Id }));

        var cancelled = await manager.PostAsJsonAsync($"/api/dispatches/{dispatch.GetProperty("id").GetGuid()}/cancel", new { reason = "Wrong vehicle" });

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var reopened = Assert.Single(await ProposalsAsync(callId));
        Assert.Contains(ambulance.Id, JsonSerializer.Deserialize<List<Guid>>(reopened.ExcludeAmbulanceIdsJson!)!);
    }

    [Fact]
    public async Task A_diversion_opens_a_new_recommendation_for_the_call_that_lost_its_ambulance()
    {
        var others = await AmbulanceIdsAsync();
        var ambulance = await SeedAmbulanceAsync();
        var sourceCall = await SeedCallAsync(CallPriority.Low);
        var urgentCall = await SeedCallAsync(CallPriority.Critical);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        Assert.Equal(HttpStatusCode.Created,
            (await manager.PostAsJsonAsync($"/api/emergency-calls/{sourceCall}/dispatch", new { ambulance_id = ambulance.Id })).StatusCode);
        var proposal = await SettledAsync(manager, await StartAsync(manager, urgentCall, others));
        Assert.Equal("pending_approval", proposal.GetProperty("status").GetString());

        var approved = await manager.PostAsJsonAsync(
            $"/api/dispatch-proposals/{proposal.GetProperty("id").GetGuid()}/approve", new { notes = "Cardiac arrest" });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var executed = (await ProposalsAsync(urgentCall)).Single(x => x.Id == proposal.GetProperty("id").GetGuid());
        Assert.Equal(DispatchProposalStatus.Executed, executed.Status);
        Assert.Equal("Cardiac arrest", executed.ReviewNotes);
        var reopened = Assert.Single(await ProposalsAsync(sourceCall));
        Assert.NotEqual(DispatchProposalStatus.Withdrawn, reopened.Status);
    }

    [Fact]
    public async Task A_crew_can_decline_a_call_still_holding_an_old_open_recommendation()
    {
        var ambulance = await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var dispatch = await ReadAsync(await manager.PostAsJsonAsync(
            $"/api/emergency-calls/{callId}/dispatch", new { ambulance_id = ambulance.Id }));
        using (var scope = _application.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IDispatchProposalLifecycle>()
                .Open(callId, CallPriority.High, allowDiversion: true, []).Status = DispatchProposalStatus.PendingConfirmation;
            await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().SaveChangesAsync();
        }
        using var crew = await ClientAsync(ambulance.CrewEmail);

        var declined = await crew.PostAsJsonAsync($"/api/me/dispatches/{dispatch.GetProperty("id").GetGuid()}/decline", new { reason = "Flat tyre" });

        Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
        var proposals = await ProposalsAsync(callId);
        Assert.Equal(2, proposals.Count);
        Assert.Equal(DispatchProposalStatus.Withdrawn, proposals[0].Status);
    }

    [Fact]
    public async Task Sending_a_recommendation_withdrawn_mid_send_dispatches_nothing()
    {
        var others = await AmbulanceIdsAsync();
        await SeedAmbulanceAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var proposalId = (await SettledAsync(manager, await StartAsync(manager, callId, others))).GetProperty("id").GetGuid();

        HttpResponseMessage sent;
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<IDispatchProposalLifecycle>().LockCallAsync(callId);
            var send = manager.PostAsync($"/api/dispatch-proposals/{proposalId}/confirm", null);
            await WaitForLockWaiterAsync();
            await scope.ServiceProvider.GetRequiredService<IDispatchProposalLifecycle>()
                .WithdrawOpenAsync(callId, DispatchWithdrawalReason.CallChanged);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            sent = await send;
        }

        Assert.Equal(HttpStatusCode.Conflict, sent.StatusCode);
        using var check = _application.Services.CreateScope();
        var state = check.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.False(await state.Dispatches.AnyAsync(x => x.EmergencyCallId == callId));
        Assert.Equal(CallStatus.Received, (await state.EmergencyCalls.FindAsync(callId))!.Status);
    }

    [Fact]
    public async Task Each_failed_recommendation_for_a_call_rings_its_own_bell()
    {
        var others = await AmbulanceIdsAsync();
        var callId = await SeedCallAsync();
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
            {
                emergency_call_id = callId, allow_diversion = false, exclude_ambulance_ids = others
            });
            Assert.Equal("failed", (await SettledAsync(manager, (await ReadAsync(create)).GetProperty("id").GetGuid())).GetProperty("status").GetString());
        }

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var managers = await db.StaffMembers.CountAsync(x => x.Role == StaffRole.DutyManager && x.IsActive);
        Assert.Equal(2 * managers, await db.Notifications.CountAsync(x => x.Type == NotificationType.DispatchProposalFailed && x.EntityId == callId));
    }

    private sealed class WithdrawingAgent(IServiceProvider services, Guid callId) : IDispatchAgent
    {
        public async Task<DispatchAgentRun> RunAsync(DispatchAgentRequest request, CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDispatchProposalLifecycle>()
                .WithdrawOpenAsync(callId, DispatchWithdrawalReason.DispatchedManually, cancellationToken);
            await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().SaveChangesAsync(cancellationToken);

            return new DispatchAgentRun([], [], [], DispatchOutcome.FreeAmbulanceProposed, false,
                Guid.NewGuid(), "LATE-1", 4, "Too late", null, null, []);
        }
    }

    private async Task WaitForLockWaiterAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        while (await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%emergency_calls%FOR UPDATE%'").SingleAsync() == 0)
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("The send never waited on the call lock.");
            await Task.Delay(20);
        }
    }

    private async Task<Guid> CreateCallAsync(HttpClient client, Guid key, string? details = null)
    {
        var response = await client.PostAsJsonAsync("/api/emergency-calls", new
        {
            patient_is_caller = true, latitude = 6.927079, longitude = 79.861244,
            location_accuracy_metres = 12.5, location_captured_at = DateTimeOffset.UtcNow,
            idempotency_key = key, details
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> StartAsync(HttpClient manager, Guid callId, IReadOnlyList<Guid> excluded)
    {
        var create = await manager.PostAsJsonAsync("/api/dispatch-proposals", new
        {
            emergency_call_id = callId, exclude_ambulance_ids = excluded
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        return (await ReadAsync(create)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> SettledAsync(HttpClient client, Guid proposalId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var body = await ReadAsync(await client.GetAsync($"/api/dispatch-proposals/{proposalId}"));
            if (body.GetProperty("status").GetString() != "pending") return body;
            if (DateTime.UtcNow > deadline) Assert.Fail("Dispatch proposal never left pending.");
            await Task.Delay(50);
        }
    }

    // Stands in for the agent having found an ambulance, which the shared fleet cannot promise.
    private async Task MakeReadyAsync(Guid callId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        while (await db.DispatchProposals.AnyAsync(x => x.EmergencyCallId == callId && x.Status == DispatchProposalStatus.Pending))
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("Dispatch proposal never left pending.");
            await Task.Delay(50);
        }

        var proposal = await db.DispatchProposals.SingleAsync(x => x.EmergencyCallId == callId);
        proposal.Status = DispatchProposalStatus.PendingConfirmation;
        await db.SaveChangesAsync();
    }

    private async Task<List<DispatchProposal>> ProposalsAsync(Guid callId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.DispatchProposals.AsNoTracking()
            .Where(x => x.EmergencyCallId == callId).OrderBy(x => x.CreatedAt).ToListAsync();
    }

    private async Task<DispatchProposal> OnlyProposalAsync(Guid callId) => Assert.Single(await ProposalsAsync(callId));

    private async Task<int> ProposalCountAsync(Guid callId) => (await ProposalsAsync(callId)).Count;

    private async Task<AgentWorkflow> WorkflowAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().AgentWorkflows.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<List<Guid>> AmbulanceIdsAsync()
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().Ambulances.AsNoTracking().Select(x => x.Id).ToListAsync();
    }

    private async Task<SeededAmbulance> SeedAmbulanceAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var managerId = await db.StaffMembers.Where(x => x.Email == ApiApplication.ManagerEmail).Select(x => x.Id).SingleAsync();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = $"L{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true,
            Status = AmbulanceStatus.Available,
            CurrentLatitude = 6.927079m,
            CurrentLongitude = 79.861244m,
            LocationUpdatedAt = DateTimeOffset.UtcNow
        };
        db.Ambulances.Add(ambulance);
        var emails = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            var staff = new StaffMember
            {
                Id = Guid.NewGuid(),
                Email = $"lifecycle-crew-{Guid.NewGuid():N}@carelanka.invalid",
                PasswordHash = passwords.Hash(ApiApplication.Password),
                FirstName = "Lifecycle",
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
            emails.Add(staff.Email);
        }

        await db.SaveChangesAsync();
        return new SeededAmbulance(ambulance.Id, emails[0]);
    }

    private async Task<Guid> SeedCallAsync(CallPriority priority = CallPriority.High)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(), Latitude = 6.9271m, Longitude = 79.8612m, Priority = priority, Status = CallStatus.Received
        };
        db.EmergencyCalls.Add(call);
        await db.SaveChangesAsync();
        return call.Id;
    }

    private async Task<HttpClient> PatientClientAsync()
    {
        var client = _application.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"lifecycle-{Guid.NewGuid():N}", password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Authorize(client, await ReadAsync(response));
        return client;
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiApplication.Password });
        Authorize(client, await ReadAsync(response));
        return client;
    }

    private static void Authorize(HttpClient client, JsonElement body)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("access_token").GetString());

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed record SeededAmbulance(Guid Id, string CrewEmail);
}
