namespace CareLanka.Api.Services.Equipment;

public interface IBedOccupancyPort
{
    Task<BedOccupancy> GetOccupancyAsync(Guid bedId, CancellationToken cancellationToken = default);
}

public record BedOccupancy(
    bool Occupied,
    bool MayTakeOutOfService,
    string? AssignmentStatus,
    DateTimeOffset? ReservedUntil);
