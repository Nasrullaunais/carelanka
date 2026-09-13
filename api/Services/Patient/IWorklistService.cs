using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IWorklistService
{
    Task<PagedResult<WorklistRow>> ListAsync(
        string? search,
        bool includeFinished,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
