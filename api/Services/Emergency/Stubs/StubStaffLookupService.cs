using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Emergency.Stubs;

// STUB: standing in for Staff Management's POST /staff/lookup. See STUBS.md row 5.
public sealed class StubStaffLookupService : IStaffLookupService
{
    private readonly CareLankaDbContext _db;

    public StubStaffLookupService(CareLankaDbContext db) => _db = db;

    public async Task<IReadOnlyList<StaffLookupResult>> LookupAsync(
        IReadOnlyList<Guid> staffIds,
        CancellationToken cancellationToken = default)
    {
        var staff = await _db.StaffMembers
            .AsNoTracking()
            .Where(member => staffIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, cancellationToken);

        return staffIds.Select(id => staff.TryGetValue(id, out var member)
            ? new StaffLookupResult(id, true, member.FullName, member.Role, true)
            : new StaffLookupResult(id, false, null, null, null)).ToList();
    }
}
