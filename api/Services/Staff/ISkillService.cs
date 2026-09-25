using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface ISkillService
{
    Task<IReadOnlyList<SkillDto>> ListSkillsAsync(string? search, CancellationToken ct = default);
    Task<SkillDto> CreateSkillAsync(CreateSkillRequest request, CancellationToken ct = default);
    Task<SkillDto> UpdateSkillAsync(Guid id, UpdateSkillRequest request, CancellationToken ct = default);
    Task RetireSkillAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StaffSkillDto>> ListStaffSkillsAsync(Guid staffMemberId, CancellationToken ct = default);
    Task<StaffSkillDto> GrantStaffSkillAsync(Guid staffMemberId, GrantStaffSkillRequest request, CancellationToken ct = default);
    Task<RevokeStaffSkillResponse> RevokeStaffSkillAsync(Guid staffMemberId, Guid skillId, CancellationToken ct = default);
}
