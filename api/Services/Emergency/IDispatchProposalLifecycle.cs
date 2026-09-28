using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

// Stages changes only; the caller saves.
public interface IDispatchProposalLifecycle
{
    DispatchProposal Open(Guid callId, CallPriority priority, bool allowDiversion, IReadOnlyCollection<Guid> excludeAmbulanceIds);

    Task<DispatchProposal?> WithdrawOpenAsync(Guid callId, DispatchWithdrawalReason reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> CarriedExclusionsAsync(Guid callId, CancellationToken cancellationToken = default);

    void Wake(DispatchProposal proposal);
}
