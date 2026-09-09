namespace CareLanka.Api.Services.Patient.Stubs;

// STUB — standing in for Equipment Management (M3). See STUBS.md row 1.
// Replace with a real IBedRegistryService once GET /beds exists (equipment-spec.yaml).
//
// Every ward gets the same count, which is the point: a hospital where every ward holds
// exactly six beds is visibly a fake. Returning zero would have been mistaken for the
// genuine "no beds recorded in this ward yet" state.
public sealed class StubBedRegistryService : IBedRegistryService
{
    public const int BedsPerWard = 6;

    public Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, int> counts =
            wardIds.Distinct().ToDictionary(id => id, _ => BedsPerWard);

        return Task.FromResult(counts);
    }
}
