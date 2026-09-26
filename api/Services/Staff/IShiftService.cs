using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IShiftService
{
    Task<PagedResult<ShiftSummaryDto>> ListShiftsAsync(
        ListShiftsQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ShiftSummaryDto> CreateShiftAsync(
        CreateShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<BulkShiftResponse> CreateShiftsBulkAsync(
        BulkShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<ShiftDetailDto> GetShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ShiftSummaryDto> UpdateShiftAsync(
        Guid id,
        CreateShiftRequest request,
        CancellationToken cancellationToken = default);

    Task CancelShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WardStaffingRuleDto>> GetWardStaffingRulesAsync(
        Guid wardId,
        CancellationToken cancellationToken = default);

    Task<ReplaceWardStaffingRulesResponse> ReplaceWardStaffingRulesAsync(
        Guid wardId,
        IReadOnlyList<WardStaffingRuleInput> rules,
        CancellationToken cancellationToken = default);
}
