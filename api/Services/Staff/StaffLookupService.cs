using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
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

    public async Task<IReadOnlyList<CrewCandidate>> SearchAvailableCrewAsync(
        string? search,
        CancellationToken ct = default)
    {
        var term = search?.Trim().ToLowerInvariant() ?? string.Empty;
        var staff = _db.StaffMembers.AsNoTracking()
            .Where(member => member.IsActive && member.Role == StaffRole.AmbulanceCrew)
            .Where(member => !_db.AmbulanceCrewAssignments.Any(assignment =>
                assignment.StaffMemberId == member.Id && assignment.UnassignedAt == null));

        if (term.Length > 0)
        {
            staff = staff.Where(member =>
                (member.FirstName + " " + member.LastName).ToLower().Contains(term));
        }

        return await staff.OrderBy(member => member.FirstName)
            .ThenBy(member => member.LastName)
            .Take(30)
            .Select(member => new CrewCandidate
            {
                StaffMemberId = member.Id,
                FullName = member.FirstName + " " + member.LastName
            })
            .ToListAsync(ct);
    }
}
