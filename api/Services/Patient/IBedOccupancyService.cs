using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// "Is anyone in this bed?" — the one question Equipment Management has to ask us before they
/// withdraw a bed for repair, and the answer behind <c>GET /api/beds/{id}/occupancy</c>.
/// </summary>
/// <remarks>
/// Its own interface, separate from <see cref="IBedAssignmentService"/>, for one concrete
/// reason: dependencies. Assigning a bed needs Equipment's register, and Equipment's bed
/// service asks us about occupancy — so a single service doing both would be
/// <c>BedService -> occupancy -> bed register -> BedService</c>, a circular dependency the
/// container throws on at the first request. This half reads nothing but our own
/// bed_assignments table, so nothing points back.
///
/// It also does not check that the bed exists. Equipment is holding the row when they ask;
/// the 404 for a bed nobody has heard of belongs to the endpoint, and lives in
/// <see cref="BedAssignmentService"/> where the register already is.
/// </remarks>
public interface IBedOccupancyService
{
    /// <summary>
    /// Whether a live assignment claims this bed right now. Answers "not occupied" for a bed
    /// that does not exist, which is why the endpoint checks the register first.
    /// </summary>
    Task<BedOccupancyStatus> GetStatusAsync(Guid bedId, CancellationToken cancellationToken = default);
}
