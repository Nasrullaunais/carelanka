using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class NoAddressGeocoder : IReverseGeocoder
{
    public Task<string?> FindAddressAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

[Collection(ApiCollection.Name)]
public sealed class SceneLookupProcessorTests
{
    private readonly ApiApplication _application;

    public SceneLookupProcessorTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task The_scene_address_is_filled_in_once_and_never_overwritten()
    {
        var (callId, _) = await SeedAsync();

        await RunAsync(new AddressLookupJob(callId), geocoder: new FixedAddress("Galle Road, Colombo"));
        await RunAsync(new AddressLookupJob(callId), geocoder: new FixedAddress("Somewhere else"));

        Assert.Equal("Galle Road, Colombo", (await CallAsync(callId)).AddressLabel);
    }

    [Fact]
    public async Task A_very_long_address_is_cut_to_fit_the_column()
    {
        var (callId, _) = await SeedAsync();

        await RunAsync(new AddressLookupJob(callId), geocoder: new FixedAddress(new string('a', 900)));

        Assert.Equal(500, (await CallAsync(callId)).AddressLabel!.Length);
    }

    [Fact]
    public async Task No_address_found_leaves_the_call_without_one()
    {
        var (callId, _) = await SeedAsync();

        await RunAsync(new AddressLookupJob(callId), geocoder: new NoAddressGeocoder());

        Assert.Null((await CallAsync(callId)).AddressLabel);
    }

    [Fact]
    public async Task A_road_route_is_stored_once_with_the_position_the_ambulance_had_at_dispatch()
    {
        var (_, dispatchId) = await SeedAsync();
        var job = new RoutePlanJob(dispatchId, 6.9m, 79.85m);
        var roads = new FixedRoads(new AmbulanceTravel(4.236, 601), isStraightLine: false);

        await RunAsync(job, distances: roads);
        await RunAsync(job, distances: roads);

        var route = await RouteAsync(dispatchId);
        Assert.Equal(6.9m, route.OriginLatitude);
        Assert.Equal(79.85m, route.OriginLongitude);
        Assert.Equal(6.9271m, route.DestinationLatitude);
        Assert.Equal(4.24m, route.PlannedDistanceKm);
        Assert.Equal(11, route.PlannedDurationMinutes);
        Assert.Equal(SceneLookupProcessor.RouteProvider, route.MapsApiReference);
    }

    [Fact]
    public async Task No_route_is_stored_when_only_a_straight_line_is_known()
    {
        var (_, dispatchId) = await SeedAsync();

        await RunAsync(new RoutePlanJob(dispatchId, 6.9m, 79.85m), distances: new FixedRoads(new AmbulanceTravel(3, null), isStraightLine: true));

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.False(await db.RouteLogs.AnyAsync(route => route.DispatchId == dispatchId));
    }

    private async Task RunAsync(SceneLookupJob job, IReverseGeocoder? geocoder = null, IAmbulanceDistanceService? distances = null)
    {
        using var scope = _application.Services.CreateScope();
        var processor = new SceneLookupProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            geocoder ?? new NoAddressGeocoder(),
            distances ?? new FixedRoads(new AmbulanceTravel(1, 60), isStraightLine: false),
            NullLogger<SceneLookupProcessor>.Instance);
        await processor.ProcessAsync(job, CancellationToken.None);
    }

    private async Task<(Guid CallId, Guid DispatchId)> SeedAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true,
            Status = AmbulanceStatus.Dispatched
        };
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Priority = CallPriority.High,
            Status = CallStatus.Dispatched
        };
        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(),
            EmergencyCallId = call.Id,
            AmbulanceId = ambulance.Id,
            Status = DispatchStatus.Assigned,
            DispatchedAt = DateTimeOffset.UtcNow
        };
        db.AddRange(ambulance, call, dispatch);
        await db.SaveChangesAsync();
        return (call.Id, dispatch.Id);
    }

    private async Task<EmergencyCall> CallAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().EmergencyCalls.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private async Task<RouteLog> RouteAsync(Guid dispatchId)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().RouteLogs.AsNoTracking().SingleAsync(x => x.DispatchId == dispatchId);
    }

    private sealed class FixedAddress(string address) : IReverseGeocoder
    {
        public Task<string?> FindAddressAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(address);
    }

    private sealed class FixedRoads(AmbulanceTravel travel, bool isStraightLine) : IAmbulanceDistanceService
    {
        public Task<DistanceMeasurement> MeasureAsync(
            IReadOnlyCollection<AmbulanceLocation> ambulances,
            decimal destinationLatitude,
            decimal destinationLongitude,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DistanceMeasurement(ambulances.ToDictionary(x => x.Id, _ => travel), isStraightLine));
    }
}
