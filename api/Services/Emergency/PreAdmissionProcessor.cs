using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class PreAdmissionProcessor(
    CareLankaDbContext db,
    IPreAdmissionGateway gateway,
    TimeProvider clock,
    IOptions<EmergencyOptions> options)
{
    private readonly PreAdmissionOptions _options = options.Value.PreAdmission;

    public async Task<int> SendDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var due = await db.PreAdmissionNotices
            .Include(x => x.EmergencyCall)
            .Where(x => x.Status == PreAdmissionStatus.Queued && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var notice in due)
        {
            await SendAsync(notice, ct);
            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    private async Task SendAsync(Data.Entities.Emergency.PreAdmissionNotice notice, CancellationToken ct)
    {
        var call = notice.EmergencyCall;
        if (call.Status == CallStatus.Cancelled)
        {
            notice.Status = PreAdmissionStatus.Failed;
            notice.FailureReason = "call_cancelled";
            return;
        }

        var dispatchedAt = await db.Dispatches
            .Where(x => x.Id == notice.DispatchId)
            .Select(x => x.DispatchedAt)
            .SingleAsync(ct);
        var request = new PreAdmissionRequest(
            notice.DispatchId,
            call.CallerUserId,
            call.PatientIsCaller,
            call.PatientId,
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
                var delay = TimeSpan.FromSeconds(_options.RetryBaseSeconds * Math.Pow(3, notice.AttemptCount - 1));
                notice.NextAttemptAt = clock.GetUtcNow() + delay;
                break;
        }
    }
}
