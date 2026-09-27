using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EmergencyReportEndpointTests(ApiApplication application)
{
    [Fact]
    public async Task Response_time_and_fleet_reports_match_dispatch_history()
    {
        var day = new DateOnly(2026, 9, 20);
        var received = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        var ambulanceId = Guid.NewGuid();
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var ambulance = new Ambulance
            {
                Id = ambulanceId,
                RegistrationNumber = $"REPORT-{ambulanceId:N}"[..20],
                Status = AmbulanceStatus.Available,
                CreatedAt = received,
                UpdatedAt = received
            };
            var call = new EmergencyCall
            {
                Id = Guid.NewGuid(),
                Latitude = 6.927m,
                Longitude = 79.861m,
                LocationAccuracyMetres = 5,
                LocationCapturedAt = received,
                IdempotencyKey = Guid.NewGuid(),
                Priority = CallPriority.High,
                Status = CallStatus.Completed,
                CreatedAt = received,
                UpdatedAt = received
            };
            var dispatch = new Dispatch
            {
                Id = Guid.NewGuid(),
                EmergencyCall = call,
                Ambulance = ambulance,
                Status = DispatchStatus.HandedOver,
                DispatchedAt = received.AddMinutes(10),
                CompletedAt = received.AddHours(2),
                CreatedAt = received.AddMinutes(10),
                UpdatedAt = received.AddHours(2)
            };
            dispatch.RouteLog = new RouteLog
            {
                Id = Guid.NewGuid(),
                Dispatch = dispatch,
                OriginLatitude = 6.9m,
                OriginLongitude = 79.8m,
                DestinationLatitude = 6.927m,
                DestinationLongitude = 79.861m,
                PlannedDistanceKm = 5,
                PlannedDurationMinutes = 15,
                DepartedAt = received.AddMinutes(12),
                ArrivedAt = received.AddMinutes(30),
                CreatedAt = received,
                UpdatedAt = received
            };
            var secondCall = NewCall(received.AddHours(10), CallPriority.High);
            var reassigned = NewDispatch(secondCall, ambulance, received.AddHours(10).AddMinutes(20),
                received.AddHours(10).AddMinutes(25), DispatchStatus.Reassigned);
            var replacement = NewDispatch(secondCall, ambulance, received.AddHours(10).AddMinutes(30),
                received.AddHours(11), DispatchStatus.HandedOver);
            replacement.RouteLog = NewRoute(replacement, received.AddHours(10).AddMinutes(50));
            var boundaryCall = NewCall(received.AddDays(-1), CallPriority.Low);
            var boundaryDispatch = NewDispatch(boundaryCall, ambulance, received.AddHours(-9),
                received.AddHours(-7), DispatchStatus.HandedOver);
            db.Dispatches.AddRange(dispatch, reassigned, replacement, boundaryDispatch);
            db.AmbulanceStatusHistory.AddRange(
                Status(ambulance, received.AddHours(-8), AmbulanceStatus.Available),
                Status(ambulance, received.AddMinutes(10), AmbulanceStatus.Dispatched),
                Status(ambulance, received.AddHours(2), AmbulanceStatus.Available),
                Status(ambulance, received.AddHours(4), AmbulanceStatus.OutOfService),
                Status(ambulance, received.AddHours(8), AmbulanceStatus.Available),
                Status(ambulance, received.AddHours(10).AddMinutes(20), AmbulanceStatus.Dispatched),
                Status(ambulance, received.AddHours(11), AmbulanceStatus.Available));
            await db.SaveChangesAsync();
            await db.EmergencyCalls.Where(item => item.Id == call.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CreatedAt, received));
            await db.EmergencyCalls.Where(item => item.Id == secondCall.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CreatedAt, received.AddHours(10)));
        }

        using var client = await ManagerClientAsync();
        var response = await client.GetAsync($"/api/reports/emergency/response-times?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}&priority=high");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = Assert.Single(body.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(2, row.GetProperty("call_count").GetInt32());
        Assert.Equal(15, row.GetProperty("median_minutes_to_dispatch").GetDouble());
        Assert.Equal(20, row.GetProperty("median_minutes_to_arrival").GetDouble());

        var fleet = await client.GetAsync($"/api/reports/emergency/fleet-utilisation?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, fleet.StatusCode);
        using var fleetBody = JsonDocument.Parse(await fleet.Content.ReadAsStringAsync());
        var fleetRow = fleetBody.RootElement.GetProperty("rows").EnumerateArray()
            .Single(item => item.GetProperty("ambulance_id").GetGuid() == ambulanceId);
        Assert.Equal(4, fleetRow.GetProperty("run_count").GetInt32());
        Assert.Equal(3.42, fleetRow.GetProperty("hours_committed").GetDouble());
        Assert.Equal(4, fleetRow.GetProperty("out_of_service_hours").GetDouble());
        Assert.Equal(0.73, fleetRow.GetProperty("idle_share").GetDouble());
    }

    [Fact]
    public async Task Agent_report_excludes_pending_and_failed_proposals_from_human_agreement_rate()
    {
        var day = new DateOnly(2026, 9, 19);
        var at = new DateTimeOffset(2026, 9, 19, 8, 0, 0, TimeSpan.Zero);
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var call = NewCall(at, CallPriority.High);
            var confirmedWorkflow = Workflow(at, passed: true);
            var rejectedWorkflow = Workflow(at.AddMinutes(1), passed: true);
            var pendingWorkflow = Workflow(at.AddMinutes(3), passed: true);
            var failedWorkflow = Workflow(at.AddMinutes(4), passed: false);
            db.EmergencyCalls.Add(call);
            db.AgentWorkflows.AddRange(confirmedWorkflow, rejectedWorkflow, pendingWorkflow, failedWorkflow);
            db.DispatchProposals.AddRange(
                Proposal(call, confirmedWorkflow, DispatchProposalStatus.Executed, at, at.AddSeconds(30)),
                Proposal(call, rejectedWorkflow, DispatchProposalStatus.Rejected, at.AddMinutes(1), at.AddMinutes(2)),
                Proposal(call, pendingWorkflow, DispatchProposalStatus.PendingConfirmation, at.AddMinutes(3), null),
                Proposal(call, failedWorkflow, DispatchProposalStatus.Failed, at.AddMinutes(4), null));
            await db.SaveChangesAsync();
            await db.DispatchProposals.Where(proposal => proposal.EmergencyCallId == call.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(proposal => proposal.CreatedAt, at));
        }

        using var client = await ManagerClientAsync();
        var response = await client.GetAsync($"/api/reports/emergency/agent-performance?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(4, body.RootElement.GetProperty("proposals_raised").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("confirmed").GetInt32());
        Assert.Equal(0.5, body.RootElement.GetProperty("confirmed_without_change_rate").GetDouble());
        Assert.Equal(0.25, body.RootElement.GetProperty("validation_failure_rate").GetDouble());
        Assert.Equal(1, body.RootElement.GetProperty("no_ambulance_available_count").GetInt32());
        Assert.Equal(30, body.RootElement.GetProperty("median_seconds_proposal_to_confirm").GetDouble());
        Assert.Equal(1, body.RootElement.GetProperty("rejection_reasons").GetProperty("ambulance_unsuitable").GetInt32());
    }

    [Fact]
    public async Task Reversed_report_range_is_rejected()
    {
        using var client = await ManagerClientAsync();
        var response = await client.GetAsync("/api/reports/emergency/agent-performance?from=2026-09-21&to=2026-09-20");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> ManagerClientAsync()
    {
        var client = application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiApplication.ManagerEmail,
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    private static EmergencyCall NewCall(DateTimeOffset createdAt, CallPriority priority) => new()
    {
        Id = Guid.NewGuid(), Latitude = 6.927m, Longitude = 79.861m, LocationAccuracyMetres = 5,
        LocationCapturedAt = createdAt, IdempotencyKey = Guid.NewGuid(), Priority = priority,
        Status = CallStatus.Completed, CreatedAt = createdAt, UpdatedAt = createdAt
    };

    private static Dispatch NewDispatch(EmergencyCall call, Ambulance ambulance, DateTimeOffset dispatchedAt,
        DateTimeOffset completedAt, DispatchStatus status) => new()
    {
        Id = Guid.NewGuid(), EmergencyCall = call, Ambulance = ambulance, Status = status,
        DispatchedAt = dispatchedAt, CompletedAt = completedAt, CreatedAt = dispatchedAt, UpdatedAt = completedAt
    };

    private static RouteLog NewRoute(Dispatch dispatch, DateTimeOffset arrivedAt) => new()
    {
        Id = Guid.NewGuid(), Dispatch = dispatch, OriginLatitude = 6.9m, OriginLongitude = 79.8m,
        DestinationLatitude = 6.927m, DestinationLongitude = 79.861m, PlannedDistanceKm = 5,
        PlannedDurationMinutes = 15, DepartedAt = dispatch.DispatchedAt, ArrivedAt = arrivedAt,
        CreatedAt = dispatch.DispatchedAt, UpdatedAt = arrivedAt
    };

    private static AmbulanceStatusHistory Status(Ambulance ambulance, DateTimeOffset startedAt, AmbulanceStatus status) =>
        new() { Id = Guid.NewGuid(), Ambulance = ambulance, Status = status, StartedAt = startedAt, CreatedAt = startedAt };

    private static AgentWorkflow Workflow(DateTimeOffset at, bool passed)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(), AgentType = AgentType.DispatchRouting, EntityType = "emergency_call",
            EntityId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), Objective = "Test report",
            Status = passed ? AgentWorkflowStatus.Executed : AgentWorkflowStatus.Failed,
            ValidationResults = DispatchWorkflowJson.Write(new[] { new CareLanka.Api.DTOs.Emergency.DispatchValidationResult
            {
                Check = "ambulance_eligible", Passed = passed, Detail = passed ? "Passed" : "Failed", CheckedAt = at
            } }),
            CreatedAt = at, UpdatedAt = at
        };
        return workflow;
    }

    private static DispatchProposal Proposal(EmergencyCall call, AgentWorkflow workflow,
        DispatchProposalStatus status, DateTimeOffset createdAt, DateTimeOffset? reviewedAt)
    {
        return new DispatchProposal
        {
            Id = Guid.NewGuid(), WorkflowId = workflow.Id, EmergencyCall = call, CallPriority = call.Priority,
            Status = status,
            Outcome = status == DispatchProposalStatus.Failed ? DispatchOutcome.NoAmbulanceAvailable : DispatchOutcome.FreeAmbulanceProposed,
            RejectionReason = status == DispatchProposalStatus.Rejected ? DispatchRejectionReason.AmbulanceUnsuitable : null,
            ReviewedAt = reviewedAt, CreatedAt = createdAt, UpdatedAt = reviewedAt ?? createdAt
        };
    }
}
