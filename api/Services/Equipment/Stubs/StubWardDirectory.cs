namespace CareLanka.Api.Services.Equipment.Stubs;

// STUB - standing in for Patient Management (M4). See STUBS.md row 2.
// Replace once GET /wards exists (patient-spec.yaml).
//
// The name says "Stub ward" on purpose. A plausible invented name like "Intensive Care"
// would be indistinguishable from a real one on screen, and would still be there at the
// demo. The trailing fragment of the id keeps two different wards telling apart.
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
