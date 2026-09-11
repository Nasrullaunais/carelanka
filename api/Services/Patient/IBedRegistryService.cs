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

    /// <summary>
    /// These beds by id, in no particular order. A bed that is retired or was never registered
    /// simply contributes no row, so the result can be shorter than the request.
    /// </summary>
    /// <remarks>
    /// For labelling: an admission list shows which bed each patient is in, and one query for a
    /// page of them beats one query per row.
    /// </remarks>
    Task<IReadOnlyList<RegisteredBed>> ListBedsByIdAsync(
        IReadOnlyCollection<Guid> bedIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// One bed by id, or null when their register has no live bed with that id. A retired bed
    /// is already gone, so it reads the same as one that never existed.
    /// </summary>
    /// <remarks>
    /// <c>Find</c> and not <c>Get</c>: what a missing bed means is the caller's to decide. It is
    /// a 404 when somebody asked about that bed by id, and a 409 when it is the bed they asked
    /// us to put a patient in.
    /// </remarks>
    Task<RegisteredBed?> FindBedAsync(Guid bedId, CancellationToken cancellationToken = default);
}

/// <summary>A bed as we see it — the frame is Equipment's, the occupant is ours.</summary>
/// <param name="Id">Equipment's bed id, which our BedAssignment rows point at.</param>
/// <param name="WardId">The ward this frame stands in. Equipment stores it; we name the ward.</param>
/// <param name="BedNumber">Their label for it, unique within a ward. Displayed, never parsed.</param>
/// <param name="HasIsolation">Theirs to set. Hard rule H4: an infectious patient needs one of these.</param>
/// <param name="Condition">Theirs to set. A bed out of service is not free, however empty it is.</param>
/// <param name="CreatedAt">When the frame was registered. Theirs, and published straight through.</param>
/// <param name="UpdatedAt">When they last changed it — a repair, a move, a renumbering.</param>
/// <remarks>
/// Eight fields rather than the three capacity needed, because step 6 answers about one bed and
/// not about a ward: the candidate list displays the number, hard rule H4 reads the isolation
/// flag, and the agent's ranking will read the distance. Exactly the six
/// patient-management-plan.md 3.1 lists as "what we need from their register", and no more —
/// the asset tag and the servicing history are Equipment's business, not ours.
/// </remarks>
public readonly record struct RegisteredBed(
    Guid Id,
    Guid WardId,
    string BedNumber,
    bool HasIsolation,
    BedCondition Condition,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
