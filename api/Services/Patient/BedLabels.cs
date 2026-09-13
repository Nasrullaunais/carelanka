using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

public readonly record struct BedLabel(string WardName, string BedNumber)
{
    public static readonly IReadOnlyDictionary<Guid, BedLabel> None = new Dictionary<Guid, BedLabel>();

    public static readonly BedLabel Unknown = new(string.Empty, string.Empty);
}

public static class BedLabels
{
    public static async Task<IReadOnlyDictionary<Guid, BedLabel>> ByBedIdAsync(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IReadOnlyCollection<Guid> bedIds,
        CancellationToken ct = default)
    {
        if (bedIds.Count == 0)
        {
            return BedLabel.None;
        }

        var registered = await beds.ListBedsByIdAsync(bedIds.Distinct().ToList(), ct);

        var wardIds = registered.Select(bed => bed.WardId).Distinct().ToList();

        var wardNames = await db.Wards
            .AsNoTracking()
            .Where(ward => wardIds.Contains(ward.Id))
            .ToDictionaryAsync(ward => ward.Id, ward => ward.Name, ct);

        return registered.ToDictionary(
            bed => bed.Id,
            bed => new BedLabel(
                wardNames.TryGetValue(bed.WardId, out var name) ? name : string.Empty,
                bed.BedNumber));
    }

    public static async Task<IReadOnlyDictionary<Guid, BedLabel>> CurrentOrLastByAdmissionAsync(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IReadOnlyCollection<Guid> admissionIds,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (admissionIds.Count == 0)
        {
            return BedLabel.None;
        }

        var ids = admissionIds.ToList();

        var assignments = await db.BedAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.AdmissionId))
            .Select(assignment => new
            {
                assignment.AdmissionId,
                assignment.BedId,
                assignment.Status,
                assignment.OccupiedAt,
                assignment.ReleasedAt,
                assignment.CreatedAt
            })
            .ToListAsync(ct);

        if (assignments.Count == 0)
        {
            return BedLabel.None;
        }

        var chosen = assignments
            .GroupBy(assignment => assignment.AdmissionId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(assignment => assignment.Status == AssignmentStatus.Released)
                    .ThenByDescending(assignment => assignment.ReleasedAt ?? DateTimeOffset.MaxValue)
                    .ThenByDescending(assignment => assignment.OccupiedAt ?? assignment.CreatedAt)
                    .First()
                    .BedId);

        var byBedId = await ByBedIdAsync(db, beds, chosen.Values.Distinct().ToList(), ct);

        var result = new Dictionary<Guid, BedLabel>();

        foreach (var (admissionId, bedId) in chosen)
        {
            if (byBedId.TryGetValue(bedId, out var label))
            {
                result[admissionId] = label;
            }
        }

        return result;
    }

    public static async Task<IReadOnlyDictionary<Guid, BedLabel>> LiveByAdmissionAsync(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IReadOnlyCollection<Guid> admissionIds,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (admissionIds.Count == 0)
        {
            return BedLabel.None;
        }

        var ids = admissionIds.ToList();

        var live = await db.BedAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.AdmissionId))
            .Where(BedHold.LiveOn(now))
            .Select(assignment => new { assignment.AdmissionId, assignment.BedId })
            .ToListAsync(ct);

        if (live.Count == 0)
        {
            return BedLabel.None;
        }

        var byBedId = await ByBedIdAsync(
            db, beds, live.Select(claim => claim.BedId).Distinct().ToList(), ct);

        var result = new Dictionary<Guid, BedLabel>();

        foreach (var claim in live)
        {
            if (byBedId.TryGetValue(claim.BedId, out var label))
            {
                result[claim.AdmissionId] = label;
            }
        }

        return result;
    }
}
