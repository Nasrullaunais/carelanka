using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IDispatchProposalService
{
    Task<DispatchProposalSummary> StartAsync(CreateDispatchProposalRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<DispatchProposalSummary>> ListAsync(ListDispatchProposalsRequest request, CancellationToken cancellationToken = default);
    Task<DispatchProposalDetail> GetAsync(Guid proposalId, CancellationToken cancellationToken = default);
    Task<DispatchProposalDetail> ConfirmAsync(Guid proposalId, CancellationToken cancellationToken = default);
    Task<DispatchProposalDetail> ApproveAsync(Guid proposalId, ApproveDispatchProposalRequest request, CancellationToken cancellationToken = default);
    Task<DispatchProposalDetail> RejectAsync(Guid proposalId, RejectDispatchProposalRequest request, CancellationToken cancellationToken = default);
}
