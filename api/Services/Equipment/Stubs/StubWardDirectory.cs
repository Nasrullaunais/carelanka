namespace CareLanka.Api.Services.Equipment.Stubs;

// STUB: replace with Patient Management's ward directory (STUBS.md row 2).
public sealed class StubWardDirectory : IWardDirectory
{
    public Task<IReadOnlyDictionary<Guid, string>> GetWardNamesAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, string> names = wardIds
            .Distinct()
            .ToDictionary(id => id, id => $"Stub ward {id.ToString("N")[..8]}");

        return Task.FromResult(names);
    }
}
