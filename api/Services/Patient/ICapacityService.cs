using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// How full the hospital is. The two aggregate reads other components are blocked on, named
/// as integration_of_functions.md 9 publishes them.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWardService"/> on purpose. That one keeps the ward register —
/// what wards exist, what kind they are. This one answers a question no single table holds:
/// a bed is Equipment's, an occupant is ours, and an expiring hold is neither until you
/// apply the clock to it.
///
/// Emergency Management define their own <c>IPatientCapacityService</c> port over this
/// (integration_of_functions.md 4.3), the same way we hold <see cref="IBedRegistryService"/>
/// over Equipment's bed register. Two names for the two sides of one boundary, which is the
/// pattern already in the repo rather than a disagreement.
///
/// **Counts only. No patient identity crosses either of these boundaries.**
/// </remarks>
public interface ICapacityService
{
    /// <summary>Free and total beds per active ward, for Emergency's destination choice.</summary>
    Task<WardCapacitySummary> GetWardCapacityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Occupancy and care mix for one ward, for Staff Management's staffing demand.
    /// Throws NotFoundException when there is no such active ward.
    /// </summary>
    Task<WardOccupancy> GetWardOccupancyAsync(Guid wardId, CancellationToken cancellationToken = default);
}
