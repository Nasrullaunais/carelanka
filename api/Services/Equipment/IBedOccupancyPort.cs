namespace CareLanka.Api.Services.Equipment;

// Our read-only port over Patient Management's answer to "is anyone in this bed?"
// (GET /beds/{id}/occupancy, patient-spec.yaml). We own the frame; they own the occupant,
// and occupancy is the presence of one of their BedAssignment rows rather than a column
// on ours. See integration_of_functions.md 6.1.
//
// This is the mirror image of the IBedRegistryService port they defined over our register.
public interface IBedOccupancyPort
{
    /// <summary>Whether this bed may be withdrawn from service right now.</summary>
    Task<BedOccupancy> GetOccupancyAsync(Guid bedId, CancellationToken cancellationToken = default);
}

/// <summary>Patient Management's BedOccupancyStatus, narrowed to what Equipment acts on.</summary>
/// <param name="Occupied">True while a live assignment exists. A lapsed hold does not count.</param>
/// <param name="MayTakeOutOfService">The direct answer. False while a patient is in the bed or a live hold stands.</param>
/// <param name="AssignmentStatus">Their wire value, carried through untyped because their enum is not on main yet.</param>
/// <param name="ReservedUntil">When a hold lapses, if the bed is held rather than occupied.</param>
public record BedOccupancy(
    bool Occupied,
    bool MayTakeOutOfService,
    string? AssignmentStatus,
    DateTimeOffset? ReservedUntil);
