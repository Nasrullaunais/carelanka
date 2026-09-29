using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class PreAdmissionProcessor(
    CareLankaDbContext db,
    IPreAdmissionGateway gateway,
    TimeProvider clock,
    IOptions<EmergencyOptions> options,
    ILogger<PreAdmissionProcessor> logger)
{
    private readonly PreAdmissionOptions _options = options.Value.PreAdmission;

    public async Task<int> ProcessDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var due = await db.PreAdmissionNotices
            .Include(x => x.EmergencyCall)
            .Where(x => (x.Status == PreAdmissionStatus.Queued || x.Status == PreAdmissionStatus.Withdrawing)
                && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var notice in due)
        {
            if (notice.Status == PreAdmissionStatus.Withdrawing)
            {
                await WithdrawAsync(notice, ct);
            }
            else
            {
                await SendAsync(notice, ct);
            }

            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    private async Task SendAsync(Data.Entities.Emergency.PreAdmissionNotice notice, CancellationToken ct)
    {
        var call = notice.EmergencyCall;
        if (call.Status == CallStatus.Cancelled)
        {
            notice.Status = PreAdmissionStatus.Withdrawn;
            notice.WithdrawalReason = CancelReason.CallCancelled;
            return;
        }

        var dispatchedAt = await db.Dispatches
            .Where(x => x.Id == notice.DispatchId)
            .Select(x => x.DispatchedAt)
            .SingleAsync(ct);
        var patientId = call.PatientId;
        if (call.PatientIsCaller && patientId is null && call.CallerUserId is { } callerId)
        {
            patientId = await db.Patients.AsNoTracking()
                .Where(patient => patient.UserAccountId == callerId)
                .Select(patient => (Guid?)patient.Id)
                .SingleOrDefaultAsync(ct);
        }
        var request = new PreAdmissionRequest(
            notice.DispatchId,
            call.CallerUserId,
            call.PatientIsCaller && patientId is not null,
            patientId,
            dispatchedAt.AddMinutes(_options.ArrivalAllowanceMinutes),
            PreAdmissionUrgency.From(call.Priority));

        var outcome = await gateway.SendAsync(request, ct);
        notice.AttemptCount++;
        switch (outcome)
        {
            case PreAdmissionOutcome.Created or PreAdmissionOutcome.AlreadyExists:
                notice.Status = PreAdmissionStatus.Sent;
                notice.SentAt = clock.GetUtcNow();
                break;
            case PreAdmissionOutcome.Rejected:
                notice.Status = PreAdmissionStatus.Failed;
                notice.FailureReason = "rejected";
                break;
            case var _ when notice.AttemptCount >= _options.MaxAttempts:
                notice.Status = PreAdmissionStatus.Failed;
                notice.FailureReason = "gave_up";
                break;
            default:
                ScheduleRetry(notice);
                break;
        }
    }

    private async Task WithdrawAsync(Data.Entities.Emergency.PreAdmissionNotice notice, CancellationToken ct)
    {
        var outcome = await gateway.WithdrawAsync(notice.DispatchId, notice.WithdrawalReason!.Value, ct);
        notice.AttemptCount++;
        switch (outcome)
        {
            case PreAdmissionWithdrawalOutcome.Withdrawn:
                notice.Status = PreAdmissionStatus.Withdrawn;
                break;
            case PreAdmissionWithdrawalOutcome.Rejected:
                FailWithdrawal(notice, "rejected");
                break;
            case var _ when notice.AttemptCount >= _options.MaxAttempts:
                FailWithdrawal(notice, "gave_up");
                break;
            default:
                ScheduleRetry(notice);
                break;
        }
    }

    private void FailWithdrawal(Data.Entities.Emergency.PreAdmissionNotice notice, string reason)
    {
        notice.Status = PreAdmissionStatus.WithdrawalFailed;
        notice.FailureReason = reason;
        logger.LogWarning(
            "Pre-admission for dispatch {DispatchId} could not be withdrawn ({Reason}); it needs a manual cancel.",
            notice.DispatchId, reason);
    }

    private void ScheduleRetry(Data.Entities.Emergency.PreAdmissionNotice notice)
    {
        var delay = TimeSpan.FromSeconds(_options.RetryBaseSeconds * Math.Pow(3, notice.AttemptCount - 1));
        notice.NextAttemptAt = clock.GetUtcNow() + delay;
    }
}
