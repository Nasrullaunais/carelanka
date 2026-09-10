using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using Microsoft.EntityFrameworkCore;
using BedEntity = CareLanka.Api.Data.Entities.Equipment.Bed;

namespace CareLanka.Api.Services.Equipment;

public sealed class BedService : IBedService
{
    // Shown when Patient Management cannot name a ward we hold beds for. Not expected in
    // normal running: it means a bed points at a ward that no longer exists.
    private const string UnknownWardName = "Unknown ward";

    private readonly CareLankaDbContext _db;
    private readonly IWardDirectory _wards;
    private readonly IBedOccupancyPort _occupancy;

    public BedService(CareLankaDbContext db, IWardDirectory wards, IBedOccupancyPort occupancy)
    {
        _db = db;
        _wards = wards;
        _occupancy = occupancy;
    }

    public async Task<PagedResult<Bed>> ListAsync(
        Guid? wardId, BedCondition? condition, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Beds.AsNoTracking();

        if (wardId is { } ward)
        {
            query = query.Where(b => b.WardId == ward);
        }

        if (condition is { } wanted)
        {
            query = query.Where(b => b.Condition == wanted);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        // Id breaks ties. Without it two beds sharing a number across wards can swap places
        // between pages, and one of them is then never returned at all.
        var rows = await query
            .OrderBy(b => b.WardId)
            .ThenBy(b => b.BedNumber)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var names = await WardNamesAsync(rows.Select(b => b.WardId).ToList(), cancellationToken);

        return PagedResult<Bed>.From(
            rows.Select(b => ToDto(b, names)).ToList(), page, pageSize, totalItems);
    }

    public async Task<Bed> CreateAsync(
        CreateBedRequest request, CancellationToken cancellationToken = default)
    {
        var assetTag = Normalise(request.AssetTag);
        var bedNumber = request.BedNumber.Trim();

        // [Required] rejects null and "", but not "   ", and trimming that leaves a bed with
        // no number at all sitting in the register.
        if (bedNumber.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        await EnsureBedNumberFreeAsync(request.WardId, bedNumber, null, cancellationToken);
        await EnsureAssetTagFreeAsync(assetTag, null, cancellationToken);

        var bed = new BedEntity
        {
            Id = Guid.NewGuid(),
            WardId = request.WardId,
            BedNumber = bedNumber,
            HasIsolation = request.HasIsolation,
            NurseStationDistance = request.NurseStationDistance,
            Condition = BedCondition.Usable,
            AssetTag = assetTag
        };

        _db.Beds.Add(bed);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(bed, cancellationToken);
    }

    public async Task<Bed> UpdateAsync(
        Guid id, UpdateBedRequest request, CancellationToken cancellationToken = default)
    {
        var bed = await GetByIdAsync(id, cancellationToken);

        // Only a change that actually withdraws the bed asks Patient Management. Re-sending
        // out_of_service on a bed already out of service is not an eviction and must not 409.
        if (request.Condition is BedCondition.OutOfService && bed.Condition != BedCondition.OutOfService)
        {
            await EnsureMayWithdrawAsync(bed, cancellationToken);
        }

        if (request.AssetTagSupplied)
        {
            var assetTag = Normalise(request.AssetTag);
            await EnsureAssetTagFreeAsync(assetTag, bed.Id, cancellationToken);
            bed.AssetTag = assetTag;
        }

        if (request.HasIsolation is { } hasIsolation)
        {
            bed.HasIsolation = hasIsolation;
        }

        if (request.NurseStationDistance is { } distance)
        {
            bed.NurseStationDistance = distance;
        }

        if (request.Condition is { } condition)
        {
            bed.Condition = condition;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(bed, cancellationToken);
    }

    public async Task<Bed> RetireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bed = await GetByIdAsync(id, cancellationToken);

        await EnsureMayWithdrawAsync(bed, cancellationToken);

        bed.IsActive = false;
        bed.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(bed, cancellationToken);
    }

    public Task<BedEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Beds.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<BedEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Bed", id);

    public async Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
    {
        if (wardIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = wardIds.Distinct().ToList();

        // Grouping is what leaves a ward with no beds out of the result rather than at zero,
        // which is the contract Patient Management's port publishes.
        return await _db.Beds
            .AsNoTracking()
            .Where(b => ids.Contains(b.WardId))
            .GroupBy(b => b.WardId)
            .Select(g => new { WardId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WardId, x => x.Count, cancellationToken);
    }

    private async Task EnsureMayWithdrawAsync(BedEntity bed, CancellationToken cancellationToken)
    {
        // Asked inside this request, before anything is written. Maintenance never evicts a
        // patient, and only Patient Management knows whether anyone is in the bed.
        var occupancy = await _occupancy.GetOccupancyAsync(bed.Id, cancellationToken);

        if (!occupancy.MayTakeOutOfService)
        {
            throw new ConflictException(MessageCode.BedOccupied, bed.BedNumber);
        }
    }

    private async Task EnsureBedNumberFreeAsync(
        Guid wardId, string bedNumber, Guid? ignoring, CancellationToken cancellationToken)
    {
        var taken = await _db.Beds.AnyAsync(
            b => b.WardId == wardId && b.BedNumber == bedNumber && (ignoring == null || b.Id != ignoring),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException(MessageCode.BedNumberTaken, bedNumber);
        }
    }

    private async Task EnsureAssetTagFreeAsync(
        string? assetTag, Guid? ignoring, CancellationToken cancellationToken)
    {
        if (assetTag is null)
        {
            return;
        }

        var taken = await _db.Beds.AnyAsync(
            b => b.AssetTag == assetTag && (ignoring == null || b.Id != ignoring), cancellationToken);

        if (taken)
        {
            throw new ConflictException(MessageCode.AssetTagTaken, assetTag);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> WardNamesAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken)
        => wardIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _wards.GetWardNamesAsync(wardIds.Distinct().ToList(), cancellationToken);

    private async Task<Bed> ToDtoAsync(BedEntity bed, CancellationToken cancellationToken)
        => ToDto(bed, await WardNamesAsync(new[] { bed.WardId }, cancellationToken));

    private static Bed ToDto(BedEntity bed, IReadOnlyDictionary<Guid, string> wardNames)
        => new()
        {
            Id = bed.Id,
            WardId = bed.WardId,
            WardName = wardNames.TryGetValue(bed.WardId, out var name) ? name : UnknownWardName,
            BedNumber = bed.BedNumber,
            HasIsolation = bed.HasIsolation,
            NurseStationDistance = bed.NurseStationDistance,
            Condition = bed.Condition,
            AssetTag = bed.AssetTag,
            CreatedAt = bed.CreatedAt,
            UpdatedAt = bed.UpdatedAt
        };

    // An asset tag of "" and one of "   " are both "no tag". Storing either would make the
    // unique index treat it as a real value that the next untagged bed then collides with.
    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
