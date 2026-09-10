namespace CareLanka.Api.Services.Equipment.Stubs;

// STUB - standing in for Patient Management (M4). See STUBS.md row 3.
// Replace once GET /beds/{id}/occupancy exists (patient-spec.yaml).
//
// This one answers "occupied", and the direction matters more than the fact that it is fake.
// Equipment asks this before withdrawing a bed, so a stub that answered "free" would let
// maintenance be scheduled on a bed with a patient in it and would pass every test we wrote.
// Answering "occupied" fails safe: taking a bed out of service is blocked until the real
// endpoint arrives, which is visible immediately rather than at the demo.
//
// STUBS.md calls this the one genuinely dangerous stub in the project.
public sealed class StubBedOccupancyPort : IBedOccupancyPort
{
    public Task<BedOccupancy> GetOccupancyAsync(
        Guid bedId, CancellationToken cancellationToken = default)
        => Task.FromResult(new BedOccupancy(
            Occupied: true,
            MayTakeOutOfService: false,
            AssignmentStatus: null,
            ReservedUntil: null));
}
