using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface ILeaveRequestService
{
    Task<LeaveRequestDetailDto> CreateLeaveRequestAsync(
        CreateLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LeaveRequestDto>> ListLeaveRequestsAsync(
        ListLeaveRequestsQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<LeaveRequestDetailDto> GetLeaveRequestDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DecideLeaveResponse> DecideLeaveRequestAsync(
        Guid id,
        DecideLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequestDto>> GetMyLeaveRequestsAsync(
        LeaveStatus? status = null,
        CancellationToken cancellationToken = default);

    Task WithdrawLeaveRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
