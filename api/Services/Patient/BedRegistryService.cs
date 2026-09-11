using System.Linq.Expressions;
using CareLanka.Api.Data;
using CareLanka.Api.Services.Equipment;
using Microsoft.EntityFrameworkCore;
using BedEntity = CareLanka.Api.Data.Entities.Equipment.Bed;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The real bed register, replacing <c>StubBedRegistryService</c>. Beds are Equipment
/// Management's table and we only ever read them.
/// </summary>
/// <remarks>
/// An adapter rather than a direct dependency at the call site, so this stays the one place
/// Patient Management touches Equipment's beds. If their side changes, one file breaks
/// instead of every caller.
/// </remarks>
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

    // Reads Equipment's table instead of delegating, because IBedService publishes no
    // by-ward listing — only a paged ListAsync that resolves ward names through their
    // IWardDirectory, which is a call straight back into us for data we already have.
    //
    // This is the read integration_of_functions.md 6.1 sanctions in as many words: "it exists
    // in Equipment's Bed table (M3's data, M4 reads) AND its condition is 'usable' (M3's data,
    // M4 reads)". Two reads, zero cross-writes. Keeping it inside this adapter is what stops
    // it spreading: no service above here knows the beds table exists. If Sethmin later adds
    // a matching method to IBedService, this becomes a delegation and nothing else moves.
    public async Task<IReadOnlyList<RegisteredBed>> ListBedsInWardsAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
    {
        if (wardIds.Count == 0)
        {
            return Array.Empty<RegisteredBed>();
        }

        var ids = wardIds.Distinct().ToList();

        // The global query filter drops retired beds, so a retired frame never reaches a
        // capacity count. That is what makes "total" mean the beds that exist today.
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

    // Their FindByIdAsync, not a query of our own: it already applies the global filter that
    // hides a retired bed, and a second copy of that read here is a second chance to forget it.
    public async Task<RegisteredBed?> FindBedAsync(
        Guid bedId, CancellationToken cancellationToken = default)
    {
        var bed = await _beds.FindByIdAsync(bedId, cancellationToken);

        return bed is null ? null : Project(bed);
    }

    /// <summary>
    /// Their row, narrowed to the eight fields we read. One definition, so a field added to their
    /// bed cannot appear in one of our answers and not the other.
    /// </summary>
    /// <remarks>
    /// An expression and not an ordinary method, because the list above runs as SQL. A method
    /// call inside <c>Select</c> is not something EF can translate, and that fails at run time
    /// rather than at compile time — the same trap the urgency sort in AdmissionService warns
    /// about. Written once here and compiled once below for the single-bed path.
    /// </remarks>
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
