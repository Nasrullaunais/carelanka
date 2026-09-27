using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

// Stands in for crew phones on the demo fleet, which has no one carrying a phone.
public sealed class DemoFleetLocationProcessor(
    CareLankaDbContext db,
    TimeProvider clock,
    IOptions<EmergencyOptions> options)
{
    // A crew phone reports every 12 seconds, so a fix this fresh came from a phone or a live run.
    public static readonly TimeSpan RealReportWindow = TimeSpan.FromMinutes(1);

    private const decimal MaxDriftDegrees = 0.0008m;

    private readonly DemoFleetOptions _options = options.Value.DemoFleet;

    public async Task<int> RefreshAsync(CancellationToken ct = default)
    {
        var parking = _options.Ambulances.ToDictionary(ambulance => ambulance.Registration, StringComparer.OrdinalIgnoreCase);
        if (parking.Count == 0)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var reportedSince = now - RealReportWindow;
        var ambulances = await db.Ambulances
            .Where(ambulance => parking.Keys.Contains(ambulance.RegistrationNumber)
                && (ambulance.LocationUpdatedAt == null || ambulance.LocationUpdatedAt < reportedSince)
                && !ambulance.Dispatches.Any(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status)))
            .ToListAsync(ct);

        foreach (var ambulance in ambulances)
        {
            var spot = parking[ambulance.RegistrationNumber];
            ambulance.CurrentLatitude = spot.Latitude + Drift();
            ambulance.CurrentLongitude = spot.Longitude + Drift();
            ambulance.LocationUpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return ambulances.Count;
    }

    private static decimal Drift()
        => Math.Round(((decimal)Random.Shared.NextDouble() * 2 - 1) * MaxDriftDegrees, 6);
}
