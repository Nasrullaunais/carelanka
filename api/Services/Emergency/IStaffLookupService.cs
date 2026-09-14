using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

public interface IStaffLookupService
{
    Task<IReadOnlyList<StaffLookupResult>> LookupAsync(
        IReadOnlyList<Guid> staffIds,
        CancellationToken cancellationToken = default);
}

public sealed record StaffLookupResult(
    Guid StaffId,
    bool Found,
    string? FullName,
    StaffRole? Role,
    bool? IsActive);
