using CareLanka.Api.Data;
using CareLanka.Api.DTOs.Staff;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class StaffLookupService : IStaffLookupService
{
    private readonly CareLankaDbContext _db;

    public StaffLookupService(CareLankaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StaffLookupResult>> LookupAsync(
        IReadOnlyList<Guid> staffIds,
        CancellationToken ct = default)
    {
        if (staffIds == null || staffIds.Count == 0)
        {
            return Array.Empty<StaffLookupResult>();
        }

        if (staffIds.Count > 100)
        {
            throw new ArgumentException("Staff IDs list cannot exceed 100 items.", nameof(staffIds));
        }

        var distinctIds = staffIds.Distinct().ToList();

        var staffMembers = await _db.StaffMembers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => distinctIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.FirstName,
                s.LastName,
                s.Role,
                s.IsActive
            })
            .ToDictionaryAsync(s => s.Id, ct);

        var results = new List<StaffLookupResult>(staffIds.Count);

        foreach (var id in staffIds)
        {
            if (staffMembers.TryGetValue(id, out var staff))
            {
                results.Add(new StaffLookupResult
                {
                    StaffId = id,
                    Found = true,
                    FullName = $"{staff.FirstName} {staff.LastName}",
                    Role = staff.Role,
                    IsActive = staff.IsActive
                });
            }
            else
            {
                results.Add(new StaffLookupResult
                {
                    StaffId = id,
                    Found = false,
                    FullName = null,
                    Role = null,
                    IsActive = null
                });
            }
        }

        return results;
    }
}
