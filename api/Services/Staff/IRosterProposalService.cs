using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

/// <summary>
/// Service managing the lifecycle of AI Roster Proposals: creation, listing, detail viewing,
/// human approval with atomic database application, rejection, and constraint revision.
/// </summary>
public interface IRosterProposalService
{
    Task<PagedResult<RosterProposalSummary>> ListProposalsAsync(
        ListRosterProposalsQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<RosterProposalDetail> GetProposalDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<RosterProposalSummary> CreateProposalAsync(
        CreateRosterProposalRequest request,
        CancellationToken cancellationToken = default);

    Task<RosterProposalDetail> ApproveProposalAsync(
        Guid id,
        ApproveRosterProposalRequest request,
        CancellationToken cancellationToken = default);

    Task<RosterProposalDetail> RejectProposalAsync(
        Guid id,
        RejectRosterProposalRequest request,
        CancellationToken cancellationToken = default);

    Task<RosterProposalSummary> RequestRevisionAsync(
        Guid id,
        RequestRosterProposalRevisionRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid?> TriggerProposalIfUnderstaffedAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);
}
