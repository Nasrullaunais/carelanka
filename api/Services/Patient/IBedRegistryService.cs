using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

// Our read-only port over Equipment Management's bed register (GET /beds, equipment-spec.yaml).
// Equipment owns the beds; we never write them. Occupancy is not on their row — it is the
// presence of a live BedAssignment of ours. See integration_of_functions.md 6.1.
public interface IBedRegistryService
{
    /// <summary>How many beds each of these wards has. A ward with no beds is absent from the result, not zero.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every live bed in these wards, with the two facts capacity needs: which ward it is in
    /// and whether it is usable. Retired beds are already gone. A ward with no beds simply
    /// contributes no rows.
    /// </summary>
    /// <remarks>
    /// Ids and not just counts, because "free" is a fact about a bed and not about a ward:
    /// it needs joining to our own BedAssignment rows one bed at a time.
    /// </remarks>
    Task<IReadOnlyList<RegisteredBed>> ListBedsInWardsAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);
}

/// <summary>A bed as capacity sees it — the frame is Equipment's, the occupant is ours.</summary>
/// <param name="Id">Equipment's bed id, which our BedAssignment rows point at.</param>
/// <param name="WardId">The ward this frame stands in. Equipment stores it; we name the ward.</param>
/// <param name="Condition">Theirs to set. A bed out of service is not free, however empty it is.</param>
public readonly record struct RegisteredBed(Guid Id, Guid WardId, BedCondition Condition);
