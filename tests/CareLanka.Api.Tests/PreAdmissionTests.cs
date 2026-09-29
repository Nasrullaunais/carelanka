using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class RecordingPreAdmissionGateway(Guid dispatchId) : IPreAdmissionGateway
{
    private readonly Queue<PreAdmissionOutcome> _script = new();

    public List<PreAdmissionRequest> Sent { get; } = [];

    private readonly Queue<PreAdmissionWithdrawalOutcome> _withdrawalScript = new();

    public PreAdmissionOutcome Default { get; set; } = PreAdmissionOutcome.Created;

    public PreAdmissionWithdrawalOutcome DefaultWithdrawal { get; set; } = PreAdmissionWithdrawalOutcome.Withdrawn;

    public List<(Guid DispatchId, CancelReason Reason)> Withdrawn { get; } = [];

    public Func<Task>? WhileSending { get; set; }

    public void ScriptWithdrawals(params PreAdmissionWithdrawalOutcome[] outcomes)
    {
        foreach (var outcome in outcomes) _withdrawalScript.Enqueue(outcome);
    }

    public void Script(params PreAdmissionOutcome[] outcomes)
    {
        foreach (var outcome in outcomes) _script.Enqueue(outcome);
    }

    public async Task<PreAdmissionOutcome> SendAsync(PreAdmissionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DispatchId != dispatchId)
        {
            return PreAdmissionOutcome.Created;
        }

        Sent.Add(request);
        if (WhileSending is not null) await WhileSending();
        return _script.Count > 0 ? _script.Dequeue() : Default;
    }

    public Task<PreAdmissionWithdrawalOutcome> WithdrawAsync(
        Guid withdrawnDispatchId, CancelReason reason, CancellationToken cancellationToken = default)
    {
        if (withdrawnDispatchId != dispatchId)
        {
            return Task.FromResult(PreAdmissionWithdrawalOutcome.Withdrawn);
        }

        Withdrawn.Add((withdrawnDispatchId, reason));
        return Task.FromResult(_withdrawalScript.Count > 0 ? _withdrawalScript.Dequeue() : DefaultWithdrawal);
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
        Assert.Equal(PreAdmissionStatus.Withdrawn, saved.Status);
        Assert.Equal(CancelReason.CallCancelled, saved.WithdrawalReason);
    }

    [Fact]
    public async Task A_withdrawing_notice_is_withdrawn_with_its_reason_and_never_sent()
    {
        var seed = await SeedAsync(noticeStatus: PreAdmissionStatus.Withdrawing, withdrawalReason: CancelReason.PatientRefused);
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId);

        await SendAsync(gateway, DateTimeOffset.UtcNow);
        await SendAsync(gateway, DateTimeOffset.UtcNow);

        Assert.Empty(gateway.Sent);
        var withdrawn = Assert.Single(gateway.Withdrawn);
        Assert.Equal(CancelReason.PatientRefused, withdrawn.Reason);
        Assert.Equal(PreAdmissionStatus.Withdrawn, (await NoticeAsync(seed.CallId)).Status);
    }

    [Fact]
    public async Task A_withdrawal_during_an_outage_is_retried_until_it_lands()
    {
        var seed = await SeedAsync(noticeStatus: PreAdmissionStatus.Withdrawing, withdrawalReason: CancelReason.FalseAlarm);
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId);
        gateway.ScriptWithdrawals(PreAdmissionWithdrawalOutcome.Unavailable);
        var now = DateTimeOffset.UtcNow;

        await SendAsync(gateway, now);
        var afterOutage = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Withdrawing, afterOutage.Status);
        Assert.True(afterOutage.NextAttemptAt > now);
        await SendAsync(gateway, now);
        Assert.Single(gateway.Withdrawn);

        await SendAsync(gateway, afterOutage.NextAttemptAt.AddSeconds(1));
        Assert.Equal(2, gateway.Withdrawn.Count);
        Assert.Equal(PreAdmissionStatus.Withdrawn, (await NoticeAsync(seed.CallId)).Status);
    }

    [Fact]
    public async Task A_withdrawal_that_keeps_failing_is_given_up_on()
    {
        var seed = await SeedAsync(noticeStatus: PreAdmissionStatus.Withdrawing, withdrawalReason: CancelReason.FalseAlarm);
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId)
        {
            DefaultWithdrawal = PreAdmissionWithdrawalOutcome.Unavailable
        };
        var when = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            await SendAsync(gateway, when);
            when = (await NoticeAsync(seed.CallId)).NextAttemptAt.AddSeconds(1);
        }

        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.WithdrawalFailed, saved.Status);
        Assert.Equal("gave_up", saved.FailureReason);
        Assert.Equal(8, saved.AttemptCount);
    }

    [Fact]
    public async Task A_refused_withdrawal_is_not_retried()
    {
        var seed = await SeedAsync(noticeStatus: PreAdmissionStatus.Withdrawing, withdrawalReason: CancelReason.FalseAlarm);
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId)
        {
            DefaultWithdrawal = PreAdmissionWithdrawalOutcome.Rejected
        };

        await SendAsync(gateway, DateTimeOffset.UtcNow);
        await SendAsync(gateway, DateTimeOffset.UtcNow.AddHours(1));

        Assert.Single(gateway.Withdrawn);
        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.WithdrawalFailed, saved.Status);
        Assert.Equal("rejected", saved.FailureReason);
    }

    [Theory]
    [InlineData(PreAdmissionStatus.Queued)]
    [InlineData(PreAdmissionStatus.Sent)]
    public async Task Requesting_a_withdrawal_queues_it_for_the_worker_and_restarts_the_attempts(PreAdmissionStatus before)
    {
        var seed = await SeedAsync(noticeStatus: before);
        await using (var scope = _application.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var notice = await db.PreAdmissionNotices.SingleAsync(x => x.EmergencyCallId == seed.CallId);
            notice.AttemptCount = 3;
            notice.NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1);
            await db.SaveChangesAsync();
        }

        await RequestWithdrawalAsync(seed.CallId, CancelReason.TreatedAtScene);

        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Withdrawing, saved.Status);
        Assert.Equal(CancelReason.TreatedAtScene, saved.WithdrawalReason);
        Assert.Equal(0, saved.AttemptCount);
        Assert.True(saved.NextAttemptAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(PreAdmissionStatus.Failed)]
    [InlineData(PreAdmissionStatus.Withdrawn)]
    [InlineData(PreAdmissionStatus.Withdrawing)]
    [InlineData(PreAdmissionStatus.WithdrawalFailed)]
    public async Task A_notice_that_is_not_waiting_or_sent_is_left_alone(PreAdmissionStatus status)
    {
        var seed = await SeedAsync(noticeStatus: status, withdrawalReason: status is PreAdmissionStatus.Failed ? null : CancelReason.FalseAlarm);

        await RequestWithdrawalAsync(seed.CallId, CancelReason.DiedAtScene);

        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(status, saved.Status);
        Assert.NotEqual(CancelReason.DiedAtScene, saved.WithdrawalReason);
    }

    [Fact]
    public async Task A_call_without_a_notice_needs_no_withdrawal()
    {
        await RequestWithdrawalAsync(Guid.NewGuid(), CancelReason.CallCancelled);
    }

    [Fact]
    public async Task A_withdrawal_requested_while_a_send_is_in_flight_is_not_overwritten()
    {
        var seed = await SeedAsync();
        var gateway = new RecordingPreAdmissionGateway(seed.DispatchId)
        {
            WhileSending = () => RequestWithdrawalAsync(seed.CallId, CancelReason.PatientRefused)
        };
        var now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => SendAsync(gateway, now));

        var saved = await NoticeAsync(seed.CallId);
        Assert.Equal(PreAdmissionStatus.Withdrawing, saved.Status);
        gateway.WhileSending = null;
        await SendAsync(gateway, now.AddSeconds(1));
        Assert.Equal(CancelReason.PatientRefused, Assert.Single(gateway.Withdrawn).Reason);
        Assert.Equal(PreAdmissionStatus.Withdrawn, (await NoticeAsync(seed.CallId)).Status);
    }

    [Theory]
    [InlineData(AdmissionStatus.AwaitingBed, PreAdmissionStatus.Withdrawn, AdmissionStatus.Cancelled)]
    [InlineData(AdmissionStatus.BedReserved, PreAdmissionStatus.Withdrawn, AdmissionStatus.Cancelled)]
    [InlineData(AdmissionStatus.Admitted, PreAdmissionStatus.WithdrawalFailed, AdmissionStatus.Admitted)]
    public async Task The_real_gateway_cancels_the_waiting_admission_and_leaves_an_admitted_one(
        AdmissionStatus admissionStatus, PreAdmissionStatus expectedNotice, AdmissionStatus expectedAdmission)
    {
        var seed = await SeedAsync(CallPriority.Critical);
        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(1));
        await SetAdmissionStatusAsync(seed.DispatchId, admissionStatus);
        await RequestWithdrawalAsync(seed.CallId, CancelReason.TreatedAtScene);

        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(2));

        Assert.Equal(expectedNotice, (await NoticeAsync(seed.CallId)).Status);
        var admission = await AdmissionAsync(seed.DispatchId);
        Assert.Equal(expectedAdmission, admission.Status);
        Assert.Equal(
            expectedAdmission == AdmissionStatus.Cancelled ? CancelReason.TreatedAtScene : null, admission.CancelReason);
    }

    [Fact]
    public async Task The_real_gateway_treats_a_missing_or_already_cancelled_admission_as_withdrawn()
    {
        var neverSent = await SeedAsync(noticeStatus: PreAdmissionStatus.Withdrawing, withdrawalReason: CancelReason.CallCancelled);
        var cancelled = await SeedAsync(CallPriority.Critical);
        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(1));
        await RequestWithdrawalAsync(cancelled.CallId, CancelReason.FalseAlarm);
        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(2));
        await SetNoticeAsync(cancelled.CallId, PreAdmissionStatus.Withdrawing);

        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(3));

        Assert.Equal(PreAdmissionStatus.Withdrawn, (await NoticeAsync(neverSent.CallId)).Status);
        Assert.Equal(PreAdmissionStatus.Withdrawn, (await NoticeAsync(cancelled.CallId)).Status);
        Assert.Equal(CancelReason.FalseAlarm, (await AdmissionAsync(cancelled.DispatchId)).CancelReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_self_report_without_a_patient_id_is_pre_admitted_using_the_account_link_or_a_provisional_patient(bool linked)
    {
        var seed = await SeedAsync();
        Guid? linkedPatientId = null;
        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var account = new PatientAccount
            {
                Id = Guid.NewGuid(), Username = $"caller{Guid.NewGuid():N}"[..20],
                PasswordHash = "not-used-in-this-test"
            };
            db.PatientAccounts.Add(account);
            if (linked)
            {
                var patient = new Patient
                {
                    Id = Guid.NewGuid(), PatientCode = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    FullName = "Emergency Caller", Gender = Gender.Female,
                    Phone = $"07{Random.Shared.NextInt64(10000000, 99999999)}",
                    UserAccountId = account.Id
                };
                db.Patients.Add(patient);
                linkedPatientId = patient.Id;
            }
            var call = await db.EmergencyCalls.SingleAsync(x => x.Id == seed.CallId);
            call.PatientIsCaller = true;
            call.CallerUserId = account.Id;
            await db.SaveChangesAsync();
        }

        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(1));
        await SendWithRealGatewayAsync(DateTimeOffset.UtcNow.AddSeconds(2));

        using var verification = _application.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var admission = Assert.Single(await context.Admissions.AsNoTracking()
            .Where(x => x.DispatchId == seed.DispatchId.ToString()).ToListAsync());
        Assert.Equal(AdmissionStatus.AwaitingBed, admission.Status);
        if (linked) Assert.Equal(linkedPatientId, admission.PatientId);
        else Assert.True(await context.Patients.AnyAsync(x => x.Id == admission.PatientId));
        Assert.Equal(PreAdmissionStatus.Sent, (await NoticeAsync(seed.CallId)).Status);
    }

    private async Task RequestWithdrawalAsync(Guid callId, CancelReason reason)
    {
        await using var scope = _application.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPreAdmissionWithdrawals>().RequestAsync(callId, reason);
        await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().SaveChangesAsync();
    }

    private async Task<Admission> AdmissionAsync(Guid dispatchId)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Admissions.AsNoTracking().SingleAsync(x => x.DispatchId == dispatchId.ToString());
    }

    private async Task SetAdmissionStatusAsync(Guid dispatchId, AdmissionStatus status)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var admission = await db.Admissions.SingleAsync(x => x.DispatchId == dispatchId.ToString());
        admission.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task SetNoticeAsync(Guid callId, PreAdmissionStatus status)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var notice = await db.PreAdmissionNotices.SingleAsync(x => x.EmergencyCallId == callId);
        notice.Status = status;
        notice.NextAttemptAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task SendAsync(RecordingPreAdmissionGateway gateway, DateTimeOffset now)
    {
        using var scope = _application.Services.CreateScope();
        var processor = new PreAdmissionProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            gateway,
            new FixedClock(now),
            Options.Create(new EmergencyOptions { PreAdmission = { BatchSize = 1000 } }),
            NullLogger<PreAdmissionProcessor>.Instance);
        await processor.ProcessDueAsync();
    }

    private async Task SendWithRealGatewayAsync(DateTimeOffset now)
    {
        using var scope = _application.Services.CreateScope();
        var processor = new PreAdmissionProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPreAdmissionGateway>(),
            new FixedClock(now),
            Options.Create(new EmergencyOptions { PreAdmission = { BatchSize = 1000 } }),
            NullLogger<PreAdmissionProcessor>.Instance);
        await processor.ProcessDueAsync();
    }

    private async Task<PreAdmissionNotice> NoticeAsync(Guid callId)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .PreAdmissionNotices.AsNoTracking().SingleAsync(x => x.EmergencyCallId == callId);
    }

    private async Task<Seed> SeedAsync(
        CallPriority priority = CallPriority.High,
        CallStatus callStatus = CallStatus.Dispatched,
        PreAdmissionStatus noticeStatus = PreAdmissionStatus.Queued,
        CancelReason? withdrawalReason = null)
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
            Status = noticeStatus, NextAttemptAt = dispatch.DispatchedAt, WithdrawalReason = withdrawalReason
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
