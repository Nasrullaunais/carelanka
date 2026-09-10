namespace CareLanka.Api.Services.Patient;

// Our read-only port over Equipment Management's bed register (GET /beds, equipment-spec.yaml).
// Equipment owns the beds; we never write them. Occupancy is not on their row — it is the
// presence of a live BedAssignment of ours. See integration_of_functions.md 6.1.
//
// Only the counting method exists so far, because Ward.total_beds is the only thing that
// needs it yet. The bed agent's candidate list widens this later.
public interface IBedRegistryService
{
    /// <summary>How many beds each of these wards has. A ward with no beds is absent from the result, not zero.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);
}
