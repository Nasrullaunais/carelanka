using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class RecordingPreAdmissionGateway(Guid dispatchId) : IPreAdmissionGateway
{
    private readonly Queue<PreAdmissionOutcome> _script = new();

    public List<PreAdmissionRequest> Sent { get; } = [];

    public PreAdmissionOutcome Default { get; set; } = PreAdmissionOutcome.Created;

    public void Script(params PreAdmissionOutcome[] outcomes)
    {
        foreach (var outcome in outcomes) _script.Enqueue(outcome);
    }

    public Task<PreAdmissionOutcome> SendAsync(PreAdmissionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DispatchId != dispatchId)
        {
            return Task.FromResult(PreAdmissionOutcome.Created);
        }

        Sent.Add(request);
        return Task.FromResult(_script.Count > 0 ? _script.Dequeue() : Default);
    }
}

[Collection(ApiCollection.Name)]
public sealed class PreAdmissionTests
{
    private readonly ApiApplication _application;

    public PreAdmissionTests(ApiApplication application) => _application = application;

    [Theory]
    [InlineData(CallPriority.Critical, AdmissionUrgency.Emergency)]
    [InlineData(CallPriority.High, AdmissionUrgency.Urgent)]
    [InlineData(CallPriority.Medium, AdmissionUrgency.Routine)]
    [InlineData(CallPriority.Low, AdmissionUrgency.Routine)]
    public void Call_priority_translates_to_the_agreed_bed_urgency(CallPriority priority, AdmissionUrgency expected)
        => Assert.Equal(expected, PreAdmissionUrgency.From(priority));

    [Fact]
    public async Task A_queued_notice_is_sent_once_with_the_translated_urgency_and_an_arrival_estimate()
    {
        var seed = await SeedAsync(CallPriority.Critical);
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId);
        var now = DateTimeOffset.UtcNow;

        await SendAsync(gateway, now);
        await SendAsync(gateway, now);

        var request = Assert.Single(gateway.Sent);
        Assert.Equal(AdmissionUrgency.Emergency, request.Urgency);
        Assert.Equal(seed.DispatchedAt.AddMinutes(30), request.ExpectedArrival, TimeSpan.FromSeconds(1));
        Assert.Null(request.PatientId);
        Assert.False(request.PatientIsCaller);
        Assert.Null(request.CallerUserId);
        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Sent, saved.Status);
        Assert.Equal(1, saved.AttemptCount);
    }

    [Fact]
    public async Task An_outage_keeps_the_notice_queued_and_leaves_the_dispatch_alone()
    {
        var seed = await SeedAsync();
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId);
        gateway.Script(PreAdmissionOutcome.Unavailable);
        var now = DateTimeOffset.UtcNow;

        await SendAsync(gateway, now);
        var afterOutage = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Queued, afterOutage.Status);
        Assert.True(afterOutage.NextAttemptAt > now);
        await SendAsync(gateway, now);
        Assert.Single(gateway.Sent);

        await SendAsync(gateway, afterOutage.NextAttemptAt.AddSeconds(1));
        Assert.Equal(PreAdmissionStatus.Sent, (await NoticeAsync(seed.CallId)).Status);
        using var scope = _application.Services.CreateScope();
        var dispatch = await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Dispatches.AsNoTracking().SingleAsync(x => x.Id == seed.DispatchId);
        Assert.Equal(DispatchStatus.Assigned, dispatch.Status);
    }

    [Fact]
    public async Task A_notice_that_keeps_failing_is_given_up_on()
    {
        var seed = await SeedAsync();
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId) { Default = PreAdmissionOutcome.Unavailable };
        var when = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            await SendAsync(gateway, when);
            when = (await NoticeAsync(seed.CallId)).NextAttemptAt.AddSeconds(1);
        }

        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Failed, saved.Status);
        Assert.Equal("gave_up", saved.FailureReason);
        Assert.Equal(8, saved.AttemptCount);
    }

    [Fact]
    public async Task A_refused_request_is_not_retried()
    {
        var seed = await SeedAsync();
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId) { Default = PreAdmissionOutcome.Rejected };

        await SendAsync(gateway, DateTimeOffset.UtcNow);

        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Failed, saved.Status);
        Assert.Equal("rejected", saved.FailureReason);
    }

    [Fact]
    public async Task An_already_existing_pre_admission_counts_as_sent()
    {
        var seed = await SeedAsync();
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId) { Default = PreAdmissionOutcome.AlreadyExists };

        await SendAsync(gateway, DateTimeOffset.UtcNow);

        Assert.Equal(PreAdmissionStatus.Sent, (await NoticeAsync(seed.CallId)).Status);
    }

    [Fact]
    public async Task The_real_gateway_creates_exactly_one_pre_admission()
    {
        var seed = await SeedAsync(CallPriority.Critical);
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        await SendWithRealGatewayAsync(now);
        await SendWithRealGatewayAsync(now);

        using var scope = _application.Services.CreateScope();
        var admissions = await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Admissions.AsNoTracking()
            .Where(x => x.DispatchId == seed.DispatchId.ToString())
            .ToListAsync();
        var admission = Assert.Single(admissions);
        Assert.Equal(AdmissionSource.Emergency, admission.Source);
        Assert.Equal(AdmissionStatus.AwaitingBed, admission.Status);
        Assert.Equal(AdmissionUrgency.Emergency, admission.Urgency);
        Assert.NotNull(admission.ExpectedArrivalAt);
        Assert.Equal(
            seed.DispatchedAt.AddMinutes(30),
            admission.ExpectedArrivalAt.Value,
            TimeSpan.FromSeconds(1));
        Assert.Equal(PreAdmissionStatus.Sent, (await NoticeAsync(seed.CallId)).Status);
    }

    [Fact]
    public async Task A_cancelled_call_is_not_pre_admitted()
    {
        var seed = await SeedAsync(callStatus: CallStatus.Cancelled);
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId);

        await SendAsync(gateway, DateTimeOffset.UtcNow);

        Assert.Empty(gateway.Sent);
        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Failed, saved.Status);
        Assert.Equal("call_cancelled", saved.FailureReason);
    }

    private async Task SendAsync(RecordingPreAdmissionGateway gateway, DateTimeOffset now)
    {
        using var scope = _application.Services.CreateScope();
        var processor = new PreAdmissionProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            gateway,
            new FixedClock(now),
            Options.Create(new EmergencyOptions { PreAdmission = { BatchSize = 1000 } }));
        await processor.SendDueAsync();
    }

    private async Task SendWithRealGatewayAsync(DateTimeOffset now)
    {
        using var scope = _application.Services.CreateScope();
        var processor = new PreAdmissionProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPreAdmissionGateway>(),
            new FixedClock(now),
            Options.Create(new EmergencyOptions { PreAdmission = { BatchSize = 1000 } }));
        await processor.SendDueAsync();
    }

    private async Task<PreAdmissionNotice> NoticeAsync(Guid callId)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .PreAdmissionNotices.AsNoTracking().SingleAsync(x => x.EmergencyCallId == callId);
    }

    private async Task<Seed> SeedAsync(CallPriority priority = CallPriority.High, CallStatus callStatus = CallStatus.Dispatched)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(), RegistrationNumber = $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true, Status = AmbulanceStatus.Dispatched
        };
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(), Latitude = 6.9271m, Longitude = 79.8612m, Priority = priority,
            Status = callStatus, PatientIsCaller = false
        };
        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(), EmergencyCallId = call.Id, AmbulanceId = ambulance.Id,
            Status = DispatchStatus.Assigned, DispatchedAt = DateTimeOffset.UtcNow
        };
        db.Ambulances.Add(ambulance);
        db.EmergencyCalls.Add(call);
        db.Dispatches.Add(dispatch);
        db.PreAdmissionNotices.Add(new PreAdmissionNotice
        {
            Id = Guid.NewGuid(), EmergencyCallId = call.Id, DispatchId = dispatch.Id,
            Status = PreAdmissionStatus.Queued, NextAttemptAt = dispatch.DispatchedAt
        });
        await db.SaveChangesAsync();
        return new Seed(call.Id, dispatch.Id, dispatch.DispatchedAt);
    }

    private sealed record Seed(Guid CallId, Guid DispatchId, DateTimeOffset DispatchedAt);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
