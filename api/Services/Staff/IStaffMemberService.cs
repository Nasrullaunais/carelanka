using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IStaffMemberService
{
    Task<StaffMemberDto> CreateStaffMemberAsync(CreateStaffMemberRequest request, CancellationToken ct = default);

    Task<PagedResult<StaffSummaryDto>> ListStaffAsync(ListStaffQueryParameters parameters, CancellationToken ct = default);

    Task<StaffMemberDetailDto> GetStaffMemberAsync(Guid id, CancellationToken ct = default);

    Task<UpdateStaffMemberResponse> UpdateStaffMemberAsync(Guid id, UpdateStaffMemberRequest request, CancellationToken ct = default);

    Task DeactivateStaffMemberAsync(Guid id, DeactivateStaffMemberRequest request, CancellationToken ct = default);

    Task<StaffMemberDto> ReactivateStaffMemberAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every distinct department already in use by an active staff member, so the
    /// creation form can offer them instead of asking the caller to retype one.</summary>
    Task<IReadOnlyList<string>> ListDepartmentsAsync(CancellationToken ct = default);
}
