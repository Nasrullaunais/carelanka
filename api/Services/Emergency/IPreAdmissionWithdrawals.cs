using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

public interface IPreAdmissionWithdrawals
{
    /// <summary>Stages the withdrawal on the shared context; the caller saves it with its own change.</summary>
    Task RequestAsync(Guid emergencyCallId, CancelReason reason, CancellationToken cancellationToken = default);
}
