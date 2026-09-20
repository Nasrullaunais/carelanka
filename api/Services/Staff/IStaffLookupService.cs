using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IStaffLookupService
{
    Task<IReadOnlyList<StaffLookupResult>> LookupAsync(
        IReadOnlyList<Guid> staffIds,
        CancellationToken ct = default);
}
