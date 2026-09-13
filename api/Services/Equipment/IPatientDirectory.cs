namespace CareLanka.Api.Services.Equipment;

// A port, like IBedOccupancyPort: it keeps Patient Management's entity out of this component.
// Equipment stores the id and asks yes or no.
public interface IPatientDirectory
{
    Task<bool> ExistsAsync(Guid patientId, CancellationToken cancellationToken = default);
}
