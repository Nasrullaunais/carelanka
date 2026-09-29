using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

// Stages changes only; the caller saves and owns the transaction.
public interface IDispatchProposalLifecycle
{
    // Anything that opens a recommendation or dispatches the call takes this first, then reads.
    Task LockCallAsync(Guid callId, CancellationToken cancellationToken = default);

    DispatchProposal Open(Guid callId, CallPriority priority, bool allowDiversion, IReadOnlyCollection<Guid> excludeAmbulanceIds);

    Task<DispatchProposal?> WithdrawOpenAsync(Guid callId, DispatchWithdrawalReason reason, CancellationToken cancellationToken = default);

    Task MarkExecutedAsync(Guid proposalId, Guid dispatchId, string? reviewNotes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> CarriedExclusionsAsync(Guid callId, CancellationToken cancellationToken = default);

    void Wake(DispatchProposal proposal);
}
