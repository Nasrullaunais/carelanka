using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AmbulanceEligibilityEndpointTests
{
    private readonly ApiApplication _application;

    public AmbulanceEligibilityEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Fleet_list_explains_why_every_ambulance_is_eligible_or_blocked()
    {
        var prefix = $"E{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        await SeedFleetAsync(prefix);
        using var client = await ManagerClientAsync();

        var response = await client.GetAsync(
            $"/api/ambulances?search={prefix}&includeRetired=true&pageSize=100&sortDir=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = body.RootElement.GetProperty("items").EnumerateArray().ToDictionary(
            row => row.GetProperty("registration_number").GetString()!,
            row => row.Clone());
        Assert.Equal(3, rows.Count);

        var eligible = rows[$"{prefix}-A"];
        Assert.True(eligible.GetProperty("is_eligible").GetBoolean());
        Assert.Equal(2, eligible.GetProperty("current_crew_count").GetInt32());
        Assert.Equal(2, eligible.GetProperty("required_crew_count").GetInt32());
        Assert.Empty(eligible.GetProperty("eligibility_block_reasons").EnumerateArray());

        Assert.Equal(
            ["inactive", "out_of_service", "insufficient_crew", "active_dispatch", "missing_location"],
            Reasons(rows[$"{prefix}-B"]));
        Assert.Equal(["stale_location"], Reasons(rows[$"{prefix}-C"]));

        using var eligibleOnly = await client.GetAsync(
            $"/api/ambulances?search={prefix}&includeRetired=true&eligibleOnly=true&pageSize=100");
        using var eligibleBody = JsonDocument.Parse(await eligibleOnly.Content.ReadAsStringAsync());
        Assert.Single(eligibleBody.RootElement.GetProperty("items").EnumerateArray());
    }

    private async Task SeedFleetAsync(string prefix)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var managerId = await db.StaffMembers
            .Where(staff => staff.Email == ApiApplication.ManagerEmail)
            .Select(staff => staff.Id)
            .SingleAsync();
        var now = DateTimeOffset.UtcNow;
        var eligible = Vehicle($"{prefix}-A", true, AmbulanceStatus.Dispatched, now);
        var blocked = Vehicle($"{prefix}-B", false, AmbulanceStatus.OutOfService, null);
        var stale = Vehicle($"{prefix}-C", true, AmbulanceStatus.Available, now.AddMinutes(-10));
        db.Ambulances.AddRange(eligible, blocked, stale);

        var crew = Enumerable.Range(0, 5).Select(index => new StaffMember
        {
            Id = Guid.NewGuid(),
            Email = $"eligibility-{Guid.NewGuid():N}@carelanka.invalid",
            PasswordHash = "not-used-by-this-test",
            FirstName = "Eligibility",
            LastName = index.ToString(),
            Role = StaffRole.AmbulanceCrew,
            IsActive = true
        }).ToArray();
        db.StaffMembers.AddRange(crew);
        db.AmbulanceCrewAssignments.AddRange(
            Assignment(eligible.Id, crew[0].Id, managerId, now),
            Assignment(eligible.Id, crew[1].Id, managerId, now),
            Assignment(blocked.Id, crew[2].Id, managerId, now),
            Assignment(stale.Id, crew[3].Id, managerId, now),
            Assignment(stale.Id, crew[4].Id, managerId, now));
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            Latitude = 6.927079m,
            Longitude = 79.861244m,
            Priority = CallPriority.High,
            Status = CallStatus.Received
        };
        db.EmergencyCalls.Add(call);
        db.Dispatches.Add(new Dispatch
        {
            Id = Guid.NewGuid(),
            EmergencyCall = call,
            Ambulance = blocked,
            Status = DispatchStatus.Assigned,
            DispatchedAt = now
        });
        await db.SaveChangesAsync();
    }

    private static Ambulance Vehicle(
        string registration,
        bool isActive,
        AmbulanceStatus status,
        DateTimeOffset? locationUpdatedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = registration,
            IsActive = isActive,
            Status = status,
            CurrentLatitude = locationUpdatedAt.HasValue ? 6.927079m : null,
            CurrentLongitude = locationUpdatedAt.HasValue ? 79.861244m : null,
            LocationUpdatedAt = locationUpdatedAt
        };

    private static AmbulanceCrewAssignment Assignment(
        Guid ambulanceId,
        Guid staffId,
        Guid managerId,
        DateTimeOffset assignedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            AmbulanceId = ambulanceId,
            StaffMemberId = staffId,
            AssignedByStaffId = managerId,
            AssignedAt = assignedAt
        };

    private static string[] Reasons(JsonElement row)
        => row.GetProperty("eligibility_block_reasons")
            .EnumerateArray().Select(reason => reason.GetString()!).ToArray();

    private async Task<HttpClient> ManagerClientAsync()
    {
        var client = _application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiApplication.ManagerEmail,
            password = ApiApplication.Password
        });
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }
}
