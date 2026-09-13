using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Patient;

public sealed class BedOccupancyService : IBedOccupancyService
{
    private readonly CareLankaDbContext _db;

    public BedOccupancyService(CareLankaDbContext db) => _db = db;

    public async Task<BedOccupancyStatus> GetStatusAsync(Guid bedId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var claim = await _db.BedAssignments
            .AsNoTracking()
            .Where(assignment => assignment.BedId == bedId)
            .Where(BedHold.LiveOn(now))
            .Select(assignment => new
            {
                assignment.Status,
                assignment.ReservedUntil
            })
            .FirstOrDefaultAsync(ct);

        return new BedOccupancyStatus
        {
            BedId = bedId,
            Occupied = claim is not null,
            AssignmentStatus = claim?.Status,

            ReservedUntil = claim?.Status == AssignmentStatus.Reserved ? claim.ReservedUntil : null,

            MayTakeOutOfService = claim is null
        };
    }
}
