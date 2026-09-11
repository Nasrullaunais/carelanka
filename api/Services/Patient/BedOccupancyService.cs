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

        // At most one row can come back: ux_bed_assignments_live_bed makes a live claim on a
        // bed unique. FirstOrDefault rather than SingleOrDefault so a hold with no expiry —
        // which the index counts as live and BedHold.LiveOn deliberately agrees with — cannot
        // turn a read into a 500.
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

            // Only meaningful for a hold. An occupied row has no expiry — that is what
            // /arrive strips when the patient physically lands in the bed.
            ReservedUntil = claim?.Status == AssignmentStatus.Reserved ? claim.ReservedUntil : null,

            // The same fact stated the way Equipment acts on it. Not a second rule: withdrawing
            // a bed is refused for exactly as long as something claims it, and never longer.
            MayTakeOutOfService = claim is null
        };
    }
}
