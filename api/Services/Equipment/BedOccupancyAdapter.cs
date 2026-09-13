using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

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

            status.AssignmentStatus is { } assignmentStatus
                ? EnumWire.ToWire(assignmentStatus)
                : null,
            status.ReservedUntil);
    }
}
