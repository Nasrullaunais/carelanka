using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

/// <summary>Where a bed is, for display. Equipment's bed number, our ward name.</summary>
/// <param name="WardName">Empty when the ward behind a historical assignment has been retired.</param>
/// <param name="BedNumber">Empty when the bed itself has been retired since.</param>
public readonly record struct BedLabel(string WardName, string BedNumber)
{
    /// <summary>Nothing to label. The ordinary case for an admission that has never held a bed.</summary>
    public static readonly IReadOnlyDictionary<Guid, BedLabel> None = new Dictionary<Guid, BedLabel>();

    /// <summary>A bed Equipment's register no longer has. Empty, not invented.</summary>
    public static readonly BedLabel Unknown = new(string.Empty, string.Empty);
}

/// <summary>
/// Turning a bed id into something a human reads. Half the answer is Equipment's - the bed
/// number - and half is ours - the ward name - so it is the same two queries wherever it is
/// needed, which by now is four services.
/// </summary>
/// <remarks>
/// Static and taking its dependencies as arguments rather than being a registered service.
/// There is no state and no rule here, only a join that three callers had each written out
/// for themselves.
/// </remarks>
public static class BedLabels
{
    /// <summary>These beds, by bed id. A bed Equipment no longer has simply contributes no entry.</summary>
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

    /// <summary>
    /// The bed each of these admissions is in right now, by admission id. An admission holding
    /// no live bed contributes no entry, which is the ordinary state of one on the board.
    /// </summary>
    /// <remarks>
    /// At most one live assignment per admission - <c>ux_bed_assignments_live_admission</c>
    /// makes that a database guarantee, so nothing here can quietly discard one. The expiry
    /// half comes from <see cref="BedHold"/>, so this agrees with every other read of "is that
    /// bed still held".
    /// </remarks>
    /// <summary>
    /// The bed they are in, or - for a visit that is over - the last bed they were in.
    /// </summary>
    /// <remarks>
    /// <b>Releasing a bed is not the same as never having had one.</b> A discharge releases the
    /// assignment, so <see cref="LiveByAdmissionAsync"/> finds nothing and the screen said
    /// "No bed" about a patient who had just spent three days in GEN-02. That is not a missing
    /// value, it is the wrong answer to the question a record is asking - which is "where were
    /// they?", not "where are they now?".
    ///
    /// Live first, always. A patient who currently holds a bed must never be labelled with an
    /// older one, so the fallback only applies where there is no live row at all.
    ///
    /// Use this for anything that displays <b>history</b>. Use <see cref="LiveByAdmissionAsync"/>
    /// for anything that answers "who is in this bed right now" - a board, a capacity count, a
    /// placement decision.
    /// </remarks>
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

        // One bed per admission: the live one if there is one, otherwise the one they were in
        // most recently. A corrected bed leaves a released row behind, and the patient was
        // never really in it - ordering by when the row was written puts the real one last.
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
