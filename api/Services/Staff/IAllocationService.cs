using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IAllocationService
{
    Task<PagedResult<AllocationDto>> ListAllocationsAsync(
        ListAllocationsQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<AllocationDto> CreateAllocationAsync(
        CreateAllocationRequest request,
        CancellationToken cancellationToken = default);

    Task<EndAllocationResponse> EndAllocationAsync(
        Guid id,
        EndAllocationRequest request,
        CancellationToken cancellationToken = default);
}
