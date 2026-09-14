using System.Linq.Expressions;
using CareLanka.Api.Data;
using CareLanka.Api.Services.Equipment;
using Microsoft.EntityFrameworkCore;
using BedEntity = CareLanka.Api.Data.Entities.Equipment.Bed;

namespace CareLanka.Api.Services.Patient;

public sealed class BedRegistryService : IBedRegistryService
{
    private readonly IBedService _beds;
    private readonly CareLankaDbContext _db;

    public BedRegistryService(IBedService beds, CareLankaDbContext db)
    {
        _beds = beds;
        _db = db;
    }

    public Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
        => _beds.CountBedsByWardAsync(wardIds, cancellationToken);

    public async Task<IReadOnlyList<RegisteredBed>> ListBedsInWardsAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
    {
        if (wardIds.Count == 0)
        {
            return Array.Empty<RegisteredBed>();
        }

        var ids = wardIds.Distinct().ToList();

        return await _db.Beds
            .AsNoTracking()
            .Where(bed => ids.Contains(bed.WardId))
            .Select(Projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RegisteredBed>> ListBedsByIdAsync(
        IReadOnlyCollection<Guid> bedIds, CancellationToken cancellationToken = default)
    {
        if (bedIds.Count == 0)
        {
            return Array.Empty<RegisteredBed>();
        }

        var ids = bedIds.Distinct().ToList();

        return await _db.Beds
            .AsNoTracking()
            .Where(bed => ids.Contains(bed.Id))
            .Select(Projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<RegisteredBed?> FindBedAsync(
        Guid bedId, CancellationToken cancellationToken = default)
    {
        var bed = await _beds.FindByIdAsync(bedId, cancellationToken);

        return bed is null ? null : Project(bed);
    }

    private static readonly Expression<Func<BedEntity, RegisteredBed>> Projection =
        bed => new RegisteredBed(
            bed.Id,
            bed.WardId,
            bed.BedNumber,
            bed.HasIsolation,
            bed.Condition,
            bed.CreatedAt,
            bed.UpdatedAt);

    private static readonly Func<BedEntity, RegisteredBed> Project = Projection.Compile();
}
