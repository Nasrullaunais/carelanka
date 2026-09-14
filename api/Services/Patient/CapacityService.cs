using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

public sealed class CapacityService : ICapacityService
{
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
        var now = DateTimeOffset.UtcNow;

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

        var ward = await _wards.GetByIdAsync(wardId, ct);

        var beds = await _beds.ListBedsInWardsAsync(new[] { ward.Id }, ct);
        var bedIds = beds.Select(bed => bed.Id).ToList();

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

            PatientsByCategory = CountByCategory(claims
                .Where(claim => claim.Status == AssignmentStatus.Occupied)
                .Select(claim => claim.Category)),

            IncomingNext2h = claims.Count(claim =>
                claim.AdmissionStatus == AdmissionStatus.BedReserved
                && IsDueWithinWindow(claim.ExpectedArrivalAt, now))
        };
    }

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

    private static bool IsDueWithinWindow(DateTimeOffset? expectedArrival, DateTimeOffset now)
        => expectedArrival is not { } due || due <= now + IncomingWindow;

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

    private sealed record Claim(
        AssignmentStatus Status,
        AdmissionCategory Category,
        AdmissionStatus AdmissionStatus,
        DateTimeOffset? ExpectedArrivalAt);
}
