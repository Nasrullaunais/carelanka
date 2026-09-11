using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

/// <summary>
/// The real answer to "is anyone in this bed?", replacing <c>StubBedOccupancyPort</c>. Reads
/// Patient Management's assignments; Equipment never writes them.
/// </summary>
/// <remarks>
/// The mirror image of <c>BedRegistryService</c>, which is the adapter Patient Management holds
/// over Equipment's bed register. Both sit in the consuming component's folder, so each side has
/// exactly one file that knows the other exists.
///
/// The stub this replaces answered "occupied" for every bed, which meant no bed could be
/// withdrawn or retired at all - deliberately, because a stub answering "free" would have let
/// maintenance be booked on a bed with a patient in it and would have passed every test.
/// STUBS.md called it the one genuinely dangerous stub in the project.
/// </remarks>
public sealed class BedOccupancyAdapter : IBedOccupancyPort
{
    private readonly IBedOccupancyService _occupancy;

    public BedOccupancyAdapter(IBedOccupancyService occupancy) => _occupancy = occupancy;

    public async Task<BedOccupancy> GetOccupancyAsync(
        Guid bedId, CancellationToken cancellationToken = default)
    {
        var status = await _occupancy.GetStatusAsync(bedId, cancellationToken);

        return new BedOccupancy(
            status.Occupied,
            status.MayTakeOutOfService,

            // Their wire value, carried through as a string because this port was written
            // before Patient Management's enum was on main. Converted here rather than leaking
            // the enum across the boundary, which is what the port is for.
            status.AssignmentStatus is { } assignmentStatus
                ? EnumWire.ToWire(assignmentStatus)
                : null,
            status.ReservedUntil);
    }
}
