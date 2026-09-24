using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AmbulanceCrewEndpointTests
{
    private readonly ApiApplication _application;

    public AmbulanceCrewEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Duty_manager_can_assign_list_and_end_a_current_crew_assignment()
    {
        using var client = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateAmbulanceAsync(client);
        var crewId = await StaffIdAsync(ApiApplication.AmbulanceEmail);

        var assigned = await client.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
        {
            staff_member_id = crewId
        });

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        using var assignedBody = JsonDocument.Parse(await assigned.Content.ReadAsStringAsync());
        Assert.Equal(ambulanceId, assignedBody.RootElement.GetProperty("ambulance_id").GetGuid());
        Assert.Equal(crewId, assignedBody.RootElement.GetProperty("staff_member_id").GetGuid());
        Assert.Equal("AmbulanceCrew Test", assignedBody.RootElement.GetProperty("full_name").GetString());

        var current = await client.GetFromJsonAsync<JsonElement>($"/api/ambulances/{ambulanceId}/crew");
        Assert.Single(current.EnumerateArray());
        using var crewClient = await AuthenticatedClientAsync(ApiApplication.AmbulanceEmail);
        using var crewView = await crewClient.GetAsync($"/api/ambulances/{ambulanceId}/crew");
        Assert.Equal(HttpStatusCode.OK, crewView.StatusCode);

        var ended = await client.DeleteAsync($"/api/ambulances/{ambulanceId}/crew/{crewId}");
        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);

        current = await client.GetFromJsonAsync<JsonElement>($"/api/ambulances/{ambulanceId}/crew");
        Assert.Empty(current.EnumerateArray());
        Assert.Equal(1, await EndedAssignmentCountAsync(ambulanceId, crewId));
    }

    [Fact]
    public async Task Ended_assignments_stop_counting_toward_readiness()
    {
        using var client = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateAmbulanceAsync(client);
        var firstCrewId = await CreateStaffAsync(StaffRole.AmbulanceCrew);
        var secondCrewId = await CreateStaffAsync(StaffRole.AmbulanceCrew);
        foreach (var crewId in new[] { firstCrewId, secondCrewId })
        {
            using var assigned = await client.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
            {
                staff_member_id = crewId
            });
            Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        }

        var registration = await RegistrationAsync(ambulanceId);
        var ready = await FleetRowAsync(client, registration);
        Assert.True(ready.GetProperty("is_eligible").GetBoolean());

        using var ended = await client.DeleteAsync($"/api/ambulances/{ambulanceId}/crew/{secondCrewId}");
        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        var noLongerReady = await FleetRowAsync(client, registration);
        Assert.False(noLongerReady.GetProperty("is_eligible").GetBoolean());
        Assert.Contains("insufficient_crew", noLongerReady.GetProperty("eligibility_block_reasons")
            .EnumerateArray().Select(reason => reason.GetString()));
    }

    [Fact]
    public async Task Non_crew_staff_cannot_be_assigned()
    {
        using var client = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateAmbulanceAsync(client);
        var doctorId = await StaffIdAsync(ApiApplication.DoctorEmail);

        var response = await client.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
        {
            staff_member_id = doctorId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cl_emg_003", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Crew_changes_require_a_duty_manager()
    {
        using var manager = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateAmbulanceAsync(manager);
        var crewId = await StaffIdAsync(ApiApplication.AmbulanceEmail);
        using var crew = await AuthenticatedClientAsync(ApiApplication.AmbulanceEmail);
        using var anonymous = _application.CreateClient();

        var unauthenticated = await anonymous.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
        {
            staff_member_id = crewId
        });
        var forbidden = await crew.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
        {
            staff_member_id = crewId
        });

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Crew_changes_are_blocked_while_the_ambulance_has_a_live_dispatch()
    {
        using var client = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateAmbulanceAsync(client);
        var crewId = await CreateStaffAsync(StaffRole.AmbulanceCrew);
        using var assigned = await client.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
        {
            staff_member_id = crewId
        });
        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        await AddLiveDispatchAsync(ambulanceId);

        var response = await client.DeleteAsync($"/api/ambulances/{ambulanceId}/crew/{crewId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cl_emg_002", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrent_requests_cannot_assign_one_crew_member_to_two_ambulances()
    {
        using var client = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var firstAmbulanceId = await CreateAmbulanceAsync(client);
        var secondAmbulanceId = await CreateAmbulanceAsync(client);
        var crewId = await CreateStaffAsync(StaffRole.AmbulanceCrew);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/ambulances/{firstAmbulanceId}/crew", new { staff_member_id = crewId }),
            client.PostAsJsonAsync($"/api/ambulances/{secondAmbulanceId}/crew", new { staff_member_id = crewId }));

        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            responses.Select(response => response.StatusCode).Order().ToArray());
    }

    [Fact]
    public async Task Duty_manager_search_finds_only_active_unassigned_ambulance_crew()
    {
        using var manager = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var ambulanceId = await CreateAmbulanceAsync(manager);
        var name = $"Findable{Guid.NewGuid():N}";
        var availableId = await CreateStaffAsync(StaffRole.AmbulanceCrew, name);
        var assignedId = await CreateStaffAsync(StaffRole.AmbulanceCrew, name);
        await CreateStaffAsync(StaffRole.AmbulanceCrew, name, isActive: false);
        await CreateStaffAsync(StaffRole.Doctor, name);

        using var assignment = await manager.PostAsJsonAsync($"/api/ambulances/{ambulanceId}/crew", new
        {
            staff_member_id = assignedId
        });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);

        using var response = await manager.GetAsync($"/api/staff/crew-candidates?search={name.ToUpperInvariant()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var candidate = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(availableId, candidate.GetProperty("staff_member_id").GetGuid());
        Assert.Contains(name, candidate.GetProperty("full_name").GetString());

        using var crew = await AuthenticatedClientAsync(ApiApplication.AmbulanceEmail);
        using var forbidden = await crew.GetAsync($"/api/staff/crew-candidates?search={name}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Database_has_both_partial_unique_indexes_and_rejects_double_assignment()
    {
        using var client = await AuthenticatedClientAsync(ApiApplication.ManagerEmail);
        var firstAmbulanceId = await CreateAmbulanceAsync(client);
        var secondAmbulanceId = await CreateAmbulanceAsync(client);
        var crewId = await CreateStaffAsync(StaffRole.AmbulanceCrew);
        var managerId = await StaffIdAsync(ApiApplication.ManagerEmail);

        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            db.AmbulanceCrewAssignments.Add(Assignment(firstAmbulanceId, crewId, managerId));
            await db.SaveChangesAsync();
        }

        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            db.AmbulanceCrewAssignments.Add(Assignment(secondAmbulanceId, crewId, managerId));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(
                AmbulanceCrewAssignmentConfiguration.CurrentStaffUniqueIndex,
                postgres.ConstraintName);
        }

        var definitions = await CrewIndexDefinitionsAsync();
        Assert.Contains(AmbulanceCrewAssignmentConfiguration.CurrentStaffUniqueIndex, definitions.Keys);
        Assert.Contains(AmbulanceCrewAssignmentConfiguration.CurrentAmbulanceStaffUniqueIndex, definitions.Keys);
        Assert.All(definitions.Values, definition =>
        {
            Assert.Contains("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE (unassigned_at IS NULL)", definition, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<Guid> CreateAmbulanceAsync(HttpClient client)
    {
        var registration = $"WP-CRW-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        using var response = await client.PostAsJsonAsync("/api/ambulances", new
        {
            registration_number = registration,
            current_latitude = 6.927079,
            current_longitude = 79.861244
        });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> StaffIdAsync(string email)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.StaffMembers.Where(staff => staff.Email == email).Select(staff => staff.Id).SingleAsync();
    }

    private async Task<string> RegistrationAsync(Guid ambulanceId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.Ambulances.Where(ambulance => ambulance.Id == ambulanceId)
            .Select(ambulance => ambulance.RegistrationNumber).SingleAsync();
    }

    private static async Task<JsonElement> FleetRowAsync(HttpClient client, string registration)
    {
        using var response = await client.GetAsync($"/api/ambulances?search={registration}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items")[0].Clone();
    }

    private async Task<Guid> CreateStaffAsync(StaffRole role, string firstName = "Phase", bool isActive = true)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(),
            Email = $"crew-{Guid.NewGuid():N}@carelanka.invalid",
            PasswordHash = "not-used-by-this-test",
            FirstName = firstName,
            LastName = "One Crew",
            Role = role,
            IsActive = isActive
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return staff.Id;
    }

    private async Task AddLiveDispatchAsync(Guid ambulanceId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var call = new EmergencyCall
        {
            Id = Guid.NewGuid(),
            PatientIsCaller = false,
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
            AmbulanceId = ambulanceId,
            Status = DispatchStatus.Assigned,
            DispatchedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static AmbulanceCrewAssignment Assignment(Guid ambulanceId, Guid crewId, Guid managerId)
        => new()
        {
            Id = Guid.NewGuid(),
            AmbulanceId = ambulanceId,
            StaffMemberId = crewId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignedByStaffId = managerId
        };

    private async Task<Dictionary<string, string>> CrewIndexDefinitionsAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE tablename = 'ambulance_crew_assignments'
              AND indexname LIKE 'ux_ambulance_crew_assignments_current_%'
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<string, string>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0), reader.GetString(1));
        }

        return result;
    }

    private async Task<int> EndedAssignmentCountAsync(Guid ambulanceId, Guid staffId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM ambulance_crew_assignments
            WHERE ambulance_id = @ambulance_id
              AND staff_member_id = @staff_member_id
              AND unassigned_at IS NOT NULL
            """;
        var ambulanceParameter = command.CreateParameter();
        ambulanceParameter.ParameterName = "ambulance_id";
        ambulanceParameter.Value = ambulanceId;
        command.Parameters.Add(ambulanceParameter);
        var staffParameter = command.CreateParameter();
        staffParameter.ParameterName = "staff_member_id";
        staffParameter.Value = staffId;
        command.Parameters.Add(staffParameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
