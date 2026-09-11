using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

public sealed class CapacityService : ICapacityService
{
    /// <summary>
    /// How far ahead "incoming" looks. Two hours, because that is the window the spec
    /// publishes and the one a shift lead can still act on.
    /// </summary>
    private static readonly TimeSpan IncomingWindow = TimeSpan.FromHours(2);

    private readonly CareLankaDbContext _db;
    private readonly IWardService _wards;
    private readonly IBedRegistryService _beds;

    public CapacityService(CareLankaDbContext db, IWardService wards, IBedRegistryService beds)
    {
        _db = db;
        _wards = wards;
        _beds = beds;
    }

    public async Task<WardCapacitySummary> GetWardCapacityAsync(CancellationToken ct = default)
    {
        // One instant for the whole answer, taken once. Reading the clock per ward would let a
        // hold lapse partway down the list, so two wards would be counted against two
        // different "nows" and generated_at would describe neither of them.
        var now = DateTimeOffset.UtcNow;

        // The global query filter drops retired wards. A ward nobody admits to is not capacity,
        // and offering one to a dispatcher would send an ambulance to a closed door.
        var wards = await _db.Wards
            .AsNoTracking()
            .OrderBy(ward => ward.Name)
            .ToListAsync(ct);

        var beds = await _beds.ListBedsInWardsAsync(wards.Select(ward => ward.Id).ToList(), ct);
        var claimed = await ClaimedBedIdsAsync(beds.Select(bed => bed.Id).ToList(), now, ct);

        var bedsByWard = beds
            .GroupBy(bed => bed.WardId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return new WardCapacitySummary
        {
            GeneratedAt = now,
            Wards = wards.Select(ward =>
            {
                var wardBeds = bedsByWard.TryGetValue(ward.Id, out var found)
                    ? found
                    : new List<RegisteredBed>();

                return new WardCapacity
                {
                    WardId = ward.Id,
                    Name = ward.Name,
                    WardType = ward.WardType,
                    GenderPolicy = ward.GenderPolicy,
                    TotalBeds = wardBeds.Count,
                    FreeBeds = wardBeds.Count(bed =>
                        bed.Condition == BedCondition.Usable && !claimed.Contains(bed.Id))
                };
            }).ToList()
        };
    }

    public async Task<WardOccupancy> GetWardOccupancyAsync(Guid wardId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Through the ward service, so a missing or retired ward is the same 404 it is
        // everywhere else rather than a real-looking ward with zero beds in it.
        var ward = await _wards.GetByIdAsync(wardId, ct);

        var beds = await _beds.ListBedsInWardsAsync(new[] { ward.Id }, ct);
        var bedIds = beds.Select(bed => bed.Id).ToList();

        // The admission behind each live claim on a bed in this ward. Status and category are
        // read off the admission, not the assignment: "is this bed claimed" and "what care does
        // the person in it need" are facts about two different rows.
        var claims = bedIds.Count == 0
            ? new List<Claim>()
            : await _db.BedAssignments
                .AsNoTracking()
                .Where(assignment => bedIds.Contains(assignment.BedId))
                .Where(BedHold.LiveOn(now))
                .Select(assignment => new Claim(
                    assignment.Status,
                    assignment.Admission.Category,
                    assignment.Admission.Status,
                    assignment.Admission.ExpectedArrivalAt))
                .ToListAsync(ct);

        return new WardOccupancy
        {
            WardId = ward.Id,
            Name = ward.Name,
            WardType = ward.WardType,
            TotalBeds = beds.Count,
            OccupiedBeds = claims.Count(claim => claim.Status == AssignmentStatus.Occupied),
            ReservedBeds = claims.Count(claim => claim.Status == AssignmentStatus.Reserved),
            OutOfServiceBeds = beds.Count(bed => bed.Condition == BedCondition.OutOfService),

            // Only people actually in a bed. Someone merely holding one has not arrived, so
            // counting them here would tell a shift lead to staff for a patient who is not
            // there — which is what IncomingNext2h says separately, and honestly.
            PatientsByCategory = CountByCategory(claims
                .Where(claim => claim.Status == AssignmentStatus.Occupied)
                .Select(claim => claim.Category)),

            IncomingNext2h = claims.Count(claim =>
                claim.AdmissionStatus == AdmissionStatus.BedReserved
                && IsDueWithinWindow(claim.ExpectedArrivalAt, now))
        };
    }

    /// <summary>
    /// Which of these beds something is holding right now. A hold past its expiry is absent, so
    /// the caller counts that bed as free with no human having done anything.
    /// </summary>
    private async Task<HashSet<Guid>> ClaimedBedIdsAsync(
        IReadOnlyCollection<Guid> bedIds, DateTimeOffset now, CancellationToken ct)
    {
        if (bedIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = bedIds.ToList();

        var claimed = await _db.BedAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.BedId))
            .Where(BedHold.LiveOn(now))
            .Select(assignment => assignment.BedId)
            .Distinct()
            .ToListAsync(ct);

        return claimed.ToHashSet();
    }

    /// <summary>Whether a held bed's patient is due inside the next two hours.</summary>
    /// <remarks>
    /// No expected arrival counts as due. A hold lapses in thirty minutes, so an admission still
    /// holding a bed with no stated arrival time is by definition arriving sooner than two hours
    /// or losing the bed. Reading null as "not incoming" would hide a walk-in waiting at the
    /// desk from the shift lead being asked to staff for them.
    ///
    /// An overdue arrival still counts, for the same reason: somebody late is still expected.
    /// </remarks>
    private static bool IsDueWithinWindow(DateTimeOffset? expectedArrival, DateTimeOffset now)
        => expectedArrival is not { } due || due <= now + IncomingWindow;

    /// <summary>
    /// The care mix, keyed by wire value, with every category present at zero rather than
    /// missing. See <see cref="WardOccupancy.PatientsByCategory"/> for why.
    /// </summary>
    private static IReadOnlyDictionary<string, int> CountByCategory(
        IEnumerable<AdmissionCategory> categories)
    {
        var counts = Enum.GetValues<AdmissionCategory>().ToDictionary(EnumWire.ToWire, _ => 0);

        foreach (var category in categories)
        {
            counts[EnumWire.ToWire(category)]++;
        }

        return counts;
    }

    /// <summary>One live claim on a bed in this ward, flattened to the four fields it is counted on.</summary>
    private sealed record Claim(
        AssignmentStatus Status,
        AdmissionCategory Category,
        AdmissionStatus AdmissionStatus,
        DateTimeOffset? ExpectedArrivalAt);
}
