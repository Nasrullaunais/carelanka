using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Emergency;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Emergency;

public sealed class SceneLookupProcessor(
    CareLankaDbContext db,
    IReverseGeocoder geocoder,
    IAmbulanceDistanceService distances,
    ILogger<SceneLookupProcessor> logger)
{
    public const string RouteProvider = "osrm";
    private const int AddressMaxLength = 500;

    public Task ProcessAsync(SceneLookupJob job, CancellationToken cancellationToken) => job switch
    {
        AddressLookupJob address => FillAddressAsync(address, cancellationToken),
        RoutePlanJob route => SaveRouteAsync(route, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(job))
    };

    private async Task FillAddressAsync(AddressLookupJob job, CancellationToken cancellationToken)
    {
        var call = await db.EmergencyCalls.SingleOrDefaultAsync(x => x.Id == job.CallId, cancellationToken);
        if (call is null || call.AddressLabel is not null)
        {
            return;
        }

        var address = await geocoder.FindAddressAsync(call.Latitude, call.Longitude, cancellationToken);
        if (address is null)
        {
            return;
        }

        call.AddressLabel = address.Length > AddressMaxLength ? address[..AddressMaxLength] : address;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveRouteAsync(RoutePlanJob job, CancellationToken cancellationToken)
    {
        var dispatch = await db.Dispatches
            .Include(x => x.EmergencyCall)
            .Include(x => x.RouteLog)
            .SingleOrDefaultAsync(x => x.Id == job.DispatchId, cancellationToken);
        if (dispatch is null || dispatch.RouteLog is not null)
        {
            return;
        }

        var call = dispatch.EmergencyCall;
        var measurement = await distances.MeasureAsync(
            [new AmbulanceLocation(dispatch.Id, job.OriginLatitude, job.OriginLongitude)],
            call.Latitude,
            call.Longitude,
            cancellationToken);
        if (measurement.IsStraightLine || !measurement.ByAmbulance.TryGetValue(dispatch.Id, out var travel) || travel.DriveSeconds is not { } seconds)
        {
            logger.LogInformation("No road route for dispatch {DispatchId}; nothing stored.", dispatch.Id);
            return;
        }

        db.RouteLogs.Add(new RouteLog
        {
            Id = Guid.NewGuid(),
            DispatchId = dispatch.Id,
            OriginLatitude = job.OriginLatitude,
            OriginLongitude = job.OriginLongitude,
            DestinationLatitude = call.Latitude,
            DestinationLongitude = call.Longitude,
            PlannedDistanceKm = Math.Round((decimal)travel.DistanceKm, 2),
            PlannedDurationMinutes = (int)Math.Ceiling(seconds / 60.0),
            MapsApiReference = RouteProvider
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
