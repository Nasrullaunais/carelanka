using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Emergency;

public sealed class PreAdmissionWithdrawals(CareLankaDbContext db, TimeProvider clock) : IPreAdmissionWithdrawals
{
    // A queued notice is withdrawn too: the worker may be sending it at this very moment.
    public async Task RequestAsync(Guid emergencyCallId, CancelReason reason, CancellationToken ct = default)
    {
        var notice = await db.PreAdmissionNotices.SingleOrDefaultAsync(x => x.EmergencyCallId == emergencyCallId, ct);
        if (notice?.Status is not (PreAdmissionStatus.Queued or PreAdmissionStatus.Sent))
        {
            return;
        }

        notice.Status = PreAdmissionStatus.Withdrawing;
        notice.WithdrawalReason = reason;
        notice.AttemptCount = 0;
        notice.NextAttemptAt = clock.GetUtcNow();
    }
}
