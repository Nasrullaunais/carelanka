using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DemoFleetLocationTests
{
    private const decimal ParkedLatitude = 6.9094m;
    private const decimal ParkedLongitude = 79.8940m;

    private readonly ApiApplication _application;

    public DemoFleetLocationTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_stale_demo_ambulance_is_reported_near_its_parking_spot()
    {
        var now = DateTimeOffset.UtcNow;
        var ambulance = await SeedAsync(now.AddHours(-3));

        await RefreshAsync(now, ambulance.RegistrationNumber);

        var saved = await LoadAsync(ambulance.Id);
        Assert.Equal(now, saved.LocationUpdatedAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.InRange(saved.CurrentLatitude!.Value, ParkedLatitude - 0.001m, ParkedLatitude + 0.001m);
        Assert.InRange(saved.CurrentLongitude!.Value, ParkedLongitude - 0.001m, ParkedLongitude + 0.001m);
    }

    [Fact]
    public async Task An_ambulance_a_crew_phone_just_reported_is_left_alone()
    {
        var now = DateTimeOffset.UtcNow;
        var reportedAt = now.AddSeconds(-20);
        var ambulance = await SeedAsync(reportedAt);

        await RefreshAsync(now, ambulance.RegistrationNumber);

        var saved = await LoadAsync(ambulance.Id);
        Assert.Equal(reportedAt, saved.LocationUpdatedAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Equal(7.0m, saved.CurrentLatitude);
    }

    [Fact]
    public async Task An_ambulance_on_a_live_run_is_left_alone()
    {
        var now = DateTimeOffset.UtcNow;
        var ambulance = await SeedAsync(now.AddHours(-3), onLiveRun: true);

        await RefreshAsync(now, ambulance.RegistrationNumber);

        Assert.Equal(7.0m, (await LoadAsync(ambulance.Id)).CurrentLatitude);
    }

    [Fact]
    public async Task An_ambulance_outside_the_demo_fleet_is_never_touched()
    {
        var now = DateTimeOffset.UtcNow;
        var ambulance = await SeedAsync(now.AddHours(-3));

        await RefreshAsync(now, "NOT-IN-FLEET");

        Assert.Equal(7.0m, (await LoadAsync(ambulance.Id)).CurrentLatitude);
    }

    private async Task RefreshAsync(DateTimeOffset now, string registration)
    {
        using var scope = _application.Services.CreateScope();
        var options = new EmergencyOptions
        {
            DemoFleet = new DemoFleetOptions
            {
                Enabled = true,
                Ambulances = [new DemoAmbulanceOptions { Registration = registration, Latitude = ParkedLatitude, Longitude = ParkedLongitude }]
            }
        };
        await new DemoFleetLocationProcessor(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(), new FixedClock(now), Options.Create(options))
            .RefreshAsync();
    }

    private async Task<Ambulance> SeedAsync(DateTimeOffset locationUpdatedAt, bool onLiveRun = false)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var ambulance = new Ambulance
        {
            Id = Guid.NewGuid(), RegistrationNumber = $"D{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            IsActive = true, Status = onLiveRun ? AmbulanceStatus.Dispatched : AmbulanceStatus.Available,
            CurrentLatitude = 7.0m, CurrentLongitude = 80.0m, LocationUpdatedAt = locationUpdatedAt
        };
        db.Ambulances.Add(ambulance);
        if (onLiveRun)
        {
            var call = new EmergencyCall
            {
                Id = Guid.NewGuid(), Latitude = 6.9271m, Longitude = 79.8612m, Priority = CallPriority.High,
                Status = CallStatus.Dispatched, PatientIsCaller = false
            };
            db.EmergencyCalls.Add(call);
            db.Dispatches.Add(new Dispatch
            {
                Id = Guid.NewGuid(), EmergencyCallId = call.Id, AmbulanceId = ambulance.Id,
                Status = DispatchStatus.Assigned, DispatchedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return ambulance;
    }

    private async Task<Ambulance> LoadAsync(Guid id)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Ambulances.AsNoTracking().SingleAsync(ambulance => ambulance.Id == id);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
