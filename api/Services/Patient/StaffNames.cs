using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

public static class StaffNames
{
    public static async Task<IReadOnlyDictionary<Guid, string>> ByIdAsync(
        CareLankaDbContext db, IEnumerable<Guid?> ids, CancellationToken ct = default)
    {
        var wanted = ids
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var staff = await db.StaffMembers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(member => wanted.Contains(member.Id))
            .Select(member => new { member.Id, member.FirstName, member.LastName })
            .ToListAsync(ct);

        return staff.ToDictionary(
            member => member.Id, member => $"{member.FirstName} {member.LastName}");
    }

    public static string? Lookup(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id is { } value && names.TryGetValue(value, out var name) ? name : null;
}
