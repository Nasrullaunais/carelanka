using CareLanka.Api.Services.Equipment;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The real bed register, replacing <c>StubBedRegistryService</c>. Delegates straight to
/// Equipment Management's <see cref="IBedService"/> — beds are their table and we only read.
/// </summary>
/// <remarks>
/// A delegating adapter rather than a direct dependency on `IBedService` at the call site,
/// so this stays the one place Patient Management touches Equipment's service. If their
/// signature changes, one file breaks instead of every caller.
/// </remarks>
public sealed class BedRegistryService : IBedRegistryService
{
    private readonly IBedService _beds;

    public BedRegistryService(IBedService beds) => _beds = beds;

    public Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
        => _beds.CountBedsByWardAsync(wardIds, cancellationToken);
}
