using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Turns the staff ids stamped on our own rows into names a person can read.
/// </summary>
/// <remarks>
/// Every "who did this" field in this component — who approved a bed, who chose the care level,
/// who cleared the patient, who took the money — was stored as a bare <c>Guid</c> and rendered
/// as one. A screen that answers "who assigned this bed?" with
/// <c>46952f9f-b751-42b8-b602-59e1b14e86e3</c> has not answered it, and the accountability the
/// ids were added for only exists once somebody can read them.
///
/// <b>The ids stay.</b> The name is sent alongside, never instead: a name is not unique, people
/// change theirs, and the id is what an audit actually turns on.
///
/// <b>Why this reads <c>staff_members</c> directly.</b> The table is Common's, and the rule is
/// that one component does not write another's. This only ever reads, and there is no staff
/// directory endpoint to read it through — Staff Management has not been built. Written as one
/// helper rather than a join in five services so that when that endpoint does arrive, swapping
/// to it is a change to this file and nothing else.
/// </remarks>
public static class StaffNames
{
    /// <summary>
    /// Display names for the ids given, skipping nulls and duplicates. An id with no matching
    /// staff member is simply absent from the result.
    /// </summary>
    /// <remarks>
    /// Absent rather than "Unknown": the caller renders the id it already has, and a made-up
    /// label would hide the fact that a row points at a staff member who no longer exists.
    /// </remarks>
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

        // IgnoreQueryFilters, because a staff member who has left is still who did the thing.
        // Without it a bed assigned last year by somebody since deactivated would lose its
        // approver's name, which is the opposite of what an audit trail is for.
        var staff = await db.StaffMembers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(member => wanted.Contains(member.Id))
            .Select(member => new { member.Id, member.FirstName, member.LastName })
            .ToListAsync(ct);

        return staff.ToDictionary(
            member => member.Id, member => $"{member.FirstName} {member.LastName}");
    }

    /// <summary>The one name behind one nullable id, or null.</summary>
    public static string? Lookup(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id is { } value && names.TryGetValue(value, out var name) ? name : null;
}
