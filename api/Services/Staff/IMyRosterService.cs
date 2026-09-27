using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IMyRosterService
{
    Task<IReadOnlyList<MyShiftDto>> GetMyShiftsAsync(
        GetMyShiftsParameters parameters,
        CancellationToken cancellationToken = default);

    Task<AllocationDto> ClockInAsync(
        Guid allocationId,
        CancellationToken cancellationToken = default);

    Task<AllocationDto> ClockOutAsync(
        Guid allocationId,
        CancellationToken cancellationToken = default);
}
