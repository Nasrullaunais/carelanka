using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class EmergencyDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("carelanka_emergency_db_tests")
        .WithUsername("carelanka_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    public CareLankaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CareLankaDbContext>();
        options.UseCareLankaDatabase(_database.GetConnectionString());
        return new CareLankaDbContext(options.Options);
    }
}

public sealed class EmergencyDatabaseTests : IClassFixture<EmergencyDatabaseFixture>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private readonly EmergencyDatabaseFixture _fixture;

    public EmergencyDatabaseTests(EmergencyDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("id", "EM-DB-01")]
    public async Task The_same_call_sent_twice_is_stored_once()
    {
        await using var db = _fixture.CreateContext();
        var key = Guid.NewGuid();
        db.EmergencyCalls.Add(NewCall(idempotencyKey: key));
        await db.SaveChangesAsync();

        db.EmergencyCalls.Add(NewCall(idempotencyKey: key));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, "ux_emergency_calls_idempotency_key");
    }

    [Fact]
    [Trait("id", "EM-DB-02")]
    public async Task Two_active_ambulances_cannot_share_a_registration()
    {
        await using var db = _fixture.CreateContext();
        var registration = NewRegistration();
        db.Ambulances.Add(NewAmbulance(registration));
        await db.SaveChangesAsync();

        db.Ambulances.Add(NewAmbulance(registration));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, "ux_ambulances_registration_number");
    }

    [Fact]
    [Trait("id", "EM-DB-02b")]
    public async Task A_retired_ambulances_registration_can_be_used_again()
    {
        await using var db = _fixture.CreateContext();
        var registration = NewRegistration();
        var retired = NewAmbulance(registration);
        db.Ambulances.Add(retired);
        await db.SaveChangesAsync();

        retired.IsActive = false;
        retired.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        db.Ambulances.Add(NewAmbulance(registration));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.Ambulances.IgnoreQueryFilters()
            .CountAsync(ambulance => ambulance.RegistrationNumber == registration));
    }

    [Theory]
    [Trait("id", "EM-DB-03")]
    [InlineData(DispatchStatus.Assigned)]
    [InlineData(DispatchStatus.Acknowledged)]
    [InlineData(DispatchStatus.EnRouteToScene)]
    [InlineData(DispatchStatus.AtScene)]
    [InlineData(DispatchStatus.TransportingToHospital)]
    public async Task One_ambulance_cannot_be_on_two_live_runs(DispatchStatus live)
    {
        await using var db = _fixture.CreateContext();
        var ambulance = NewAmbulance(NewRegistration());
        var first = NewCall();
        var second = NewCall();
        db.AddRange(ambulance, first, second);
        db.Dispatches.Add(NewDispatch(first.Id, ambulance.Id, live));
        await db.SaveChangesAsync();

        db.Dispatches.Add(NewDispatch(second.Id, ambulance.Id, DispatchStatus.Assigned));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, "ux_dispatches_active_ambulance");
    }

    [Theory]
    [Trait("id", "EM-DB-03b")]
    [InlineData(DispatchStatus.HandedOver)]
    [InlineData(DispatchStatus.ClosedAtScene)]
    [InlineData(DispatchStatus.Declined)]
    [InlineData(DispatchStatus.Cancelled)]
    [InlineData(DispatchStatus.Reassigned)]
    public async Task An_ambulance_can_take_a_new_run_once_the_last_one_is_finished(DispatchStatus finished)
    {
        await using var db = _fixture.CreateContext();
        var ambulance = NewAmbulance(NewRegistration());
        var first = NewCall();
        var second = NewCall();
        db.AddRange(ambulance, first, second);
        db.Dispatches.Add(NewDispatch(first.Id, ambulance.Id, finished));
        await db.SaveChangesAsync();

        db.Dispatches.Add(NewDispatch(second.Id, ambulance.Id, DispatchStatus.Assigned));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.Dispatches.CountAsync(dispatch => dispatch.AmbulanceId == ambulance.Id));
    }

    [Fact]
    [Trait("id", "EM-DB-04")]
    public async Task One_call_cannot_have_two_live_ambulances()
    {
        await using var db = _fixture.CreateContext();
        var call = NewCall();
        var first = NewAmbulance(NewRegistration());
        var second = NewAmbulance(NewRegistration());
        db.AddRange(call, first, second);
        db.Dispatches.Add(NewDispatch(call.Id, first.Id, DispatchStatus.EnRouteToScene));
        await db.SaveChangesAsync();

        db.Dispatches.Add(NewDispatch(call.Id, second.Id, DispatchStatus.Assigned));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, "ux_dispatches_active_emergency_call");
    }

    [Fact]
    [Trait("id", "EM-DB-04b")]
    public async Task A_call_whose_crew_declined_can_get_another_ambulance()
    {
        await using var db = _fixture.CreateContext();
        var call = NewCall();
        var first = NewAmbulance(NewRegistration());
        var second = NewAmbulance(NewRegistration());
        db.AddRange(call, first, second);
        db.Dispatches.Add(NewDispatch(call.Id, first.Id, DispatchStatus.Declined));
        await db.SaveChangesAsync();

        db.Dispatches.Add(NewDispatch(call.Id, second.Id, DispatchStatus.Assigned));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.Dispatches.CountAsync(dispatch => dispatch.EmergencyCallId == call.Id));
    }

    [Theory]
    [Trait("id", "EM-DB-05")]
    [InlineData(DispatchProposalStatus.Pending)]
    [InlineData(DispatchProposalStatus.PendingConfirmation)]
    [InlineData(DispatchProposalStatus.PendingApproval)]
    public async Task One_call_cannot_have_two_open_recommendations(DispatchProposalStatus open)
    {
        await using var db = _fixture.CreateContext();
        var call = NewCall();
        db.EmergencyCalls.Add(call);
        db.DispatchProposals.Add(NewProposal(call.Id, open));
        await db.SaveChangesAsync();

        db.DispatchProposals.Add(NewProposal(call.Id, DispatchProposalStatus.Pending));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, "ux_dispatch_proposals_open_per_call");
    }

    [Theory]
    [Trait("id", "EM-DB-05b")]
    [InlineData(DispatchProposalStatus.Rejected)]
    [InlineData(DispatchProposalStatus.Failed)]
    [InlineData(DispatchProposalStatus.Withdrawn)]
    [InlineData(DispatchProposalStatus.Executed)]
    public async Task A_new_recommendation_is_allowed_once_the_last_one_is_closed(DispatchProposalStatus closed)
    {
        await using var db = _fixture.CreateContext();
        var call = NewCall();
        db.EmergencyCalls.Add(call);
        db.DispatchProposals.Add(NewProposal(call.Id, closed));
        await db.SaveChangesAsync();

        db.DispatchProposals.Add(NewProposal(call.Id, DispatchProposalStatus.Pending));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(2, await check.DispatchProposals.CountAsync(proposal => proposal.EmergencyCallId == call.Id));
    }

    [Fact]
    [Trait("id", "EM-DB-06")]
    public async Task A_crew_member_cannot_be_on_two_ambulances_at_once()
    {
        await using var db = _fixture.CreateContext();
        var manager = NewStaff(StaffRole.DutyManager);
        var crew = NewStaff(StaffRole.AmbulanceCrew);
        var first = NewAmbulance(NewRegistration());
        var second = NewAmbulance(NewRegistration());
        db.AddRange(manager, crew, first, second);
        db.AmbulanceCrewAssignments.Add(NewCrewAssignment(first.Id, crew.Id, manager.Id));
        await db.SaveChangesAsync();

        db.AmbulanceCrewAssignments.Add(NewCrewAssignment(second.Id, crew.Id, manager.Id));

        await AssertViolation(
            () => db.SaveChangesAsync(), UniqueViolation, "ux_ambulance_crew_assignments_current_staff");
    }

    [Fact]
    [Trait("id", "EM-DB-06b")]
    public async Task A_crew_member_taken_off_one_ambulance_can_join_another()
    {
        await using var db = _fixture.CreateContext();
        var manager = NewStaff(StaffRole.DutyManager);
        var crew = NewStaff(StaffRole.AmbulanceCrew);
        var first = NewAmbulance(NewRegistration());
        var second = NewAmbulance(NewRegistration());
        db.AddRange(manager, crew, first, second);
        var old = NewCrewAssignment(first.Id, crew.Id, manager.Id);
        db.AmbulanceCrewAssignments.Add(old);
        await db.SaveChangesAsync();

        old.UnassignedAt = DateTimeOffset.UtcNow;
        old.UnassignedByStaffId = manager.Id;
        db.AmbulanceCrewAssignments.Add(NewCrewAssignment(second.Id, crew.Id, manager.Id));
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.Equal(1, await check.AmbulanceCrewAssignments
            .CountAsync(assignment => assignment.StaffMemberId == crew.Id && assignment.UnassignedAt == null));
    }

    [Theory]
    [Trait("id", "EM-DB-07")]
    [InlineData("90.000001", "79.861200", "ck_emergency_calls_latitude")]
    [InlineData("-90.000001", "79.861200", "ck_emergency_calls_latitude")]
    [InlineData("6.927100", "180.000001", "ck_emergency_calls_longitude")]
    [InlineData("6.927100", "-180.000001", "ck_emergency_calls_longitude")]
    public async Task A_call_outside_the_earths_coordinates_is_refused(string latitude, string longitude, string constraint)
    {
        await using var db = _fixture.CreateContext();
        db.EmergencyCalls.Add(NewCall(latitude: Dec(latitude), longitude: Dec(longitude)));

        await AssertViolation(() => db.SaveChangesAsync(), CheckViolation, constraint);
    }

    [Theory]
    [Trait("id", "EM-DB-07b")]
    [InlineData("90", "180")]
    [InlineData("-90", "-180")]
    [InlineData("0", "0")]
    public async Task A_call_exactly_on_the_coordinate_limits_is_accepted(string latitude, string longitude)
    {
        await using var db = _fixture.CreateContext();
        var call = NewCall(latitude: Dec(latitude), longitude: Dec(longitude));
        db.EmergencyCalls.Add(call);

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.True(await check.EmergencyCalls.AnyAsync(saved => saved.Id == call.Id));
    }

    [Theory]
    [Trait("id", "EM-DB-08")]
    [InlineData("ambulances", "status", "flying", "ck_ambulances_status")]
    [InlineData("ambulances", "status", "Available", "ck_ambulances_status")]
    [InlineData("emergency_calls", "priority", "urgent", "ck_emergency_calls_priority")]
    [InlineData("emergency_calls", "status", "lost", "ck_emergency_calls_status")]
    [InlineData("dispatches", "status", "on_the_way", "ck_dispatches_status")]
    [InlineData("dispatch_proposals", "status", "maybe", "ck_dispatch_proposals_status")]
    public async Task Made_up_status_values_are_refused_by_the_database(
        string table, string column, string value, string constraint)
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db);

        var target = table switch
        {
            "ambulances" => ids.AmbulanceId,
            "emergency_calls" => ids.CallId,
            "dispatches" => ids.DispatchId,
            _ => ids.ProposalId
        };

#pragma warning disable EF1002 // table and column come from the InlineData above, never from input.
        await AssertViolation(
            () => db.Database.ExecuteSqlRawAsync(
                $"UPDATE {table} SET {column} = @value WHERE id = @id",
                new NpgsqlParameter("value", value),
                new NpgsqlParameter("id", target)),
            CheckViolation,
            constraint);
#pragma warning restore EF1002
    }

    [Theory]
    [Trait("id", "EM-DB-09")]
    [InlineData("-0.01", 10, "ck_route_logs_distance")]
    [InlineData("5.00", -1, "ck_route_logs_duration")]
    public async Task A_route_cannot_have_a_negative_distance_or_time(string distance, int minutes, string constraint)
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db);
        db.RouteLogs.Add(NewRouteLog(ids.DispatchId, Dec(distance), minutes));

        await AssertViolation(() => db.SaveChangesAsync(), CheckViolation, constraint);
    }

    [Fact]
    [Trait("id", "EM-DB-09b")]
    public async Task A_route_of_zero_distance_and_time_is_accepted()
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db);
        db.RouteLogs.Add(NewRouteLog(ids.DispatchId, 0m, 0));

        await db.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        Assert.True(await check.RouteLogs.AnyAsync(route => route.DispatchId == ids.DispatchId));
    }

    [Fact]
    [Trait("id", "EM-DB-10")]
    public async Task An_ambulance_with_run_history_cannot_be_deleted()
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db);

        await AssertViolation(
            () => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ambulances WHERE id = @id", new NpgsqlParameter("id", ids.AmbulanceId)),
            ForeignKeyViolation,
            "fk_dispatches_ambulances_ambulance_id");
    }

    [Fact]
    [Trait("id", "EM-DB-10b")]
    public async Task A_call_with_a_run_cannot_be_deleted()
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db);

        var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlRawAsync(
            "DELETE FROM emergency_calls WHERE id = @id", new NpgsqlParameter("id", ids.CallId)));

        var postgres = Assert.IsType<PostgresException>(error);
        Assert.Equal(ForeignKeyViolation, postgres.SqlState);
    }

    [Fact]
    [Trait("id", "EM-DB-11")]
    public async Task Deleting_a_run_takes_its_crew_list_and_route_with_it()
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db, withProposal: false);
        var crew = NewStaff(StaffRole.AmbulanceCrew);
        db.StaffMembers.Add(crew);
        db.DispatchCrew.Add(new DispatchCrew
        {
            Id = Guid.NewGuid(), DispatchId = ids.DispatchId, StaffMemberId = crew.Id, CreatedAt = DateTimeOffset.UtcNow
        });
        db.RouteLogs.Add(NewRouteLog(ids.DispatchId, 4.2m, 9));
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dispatches WHERE id = @id", new NpgsqlParameter("id", ids.DispatchId));

        await using var check = _fixture.CreateContext();
        Assert.False(await check.DispatchCrew.AnyAsync(row => row.DispatchId == ids.DispatchId));
        Assert.False(await check.RouteLogs.AnyAsync(route => route.DispatchId == ids.DispatchId));
        Assert.True(await check.StaffMembers.AnyAsync(staff => staff.Id == crew.Id));
    }

    [Fact]
    [Trait("id", "EM-DB-12")]
    public async Task A_rolled_back_dispatch_leaves_no_call_run_or_ambulance_change()
    {
        var ambulance = NewAmbulance(NewRegistration());
        await using (var seed = _fixture.CreateContext())
        {
            seed.Ambulances.Add(ambulance);
            await seed.SaveChangesAsync();
        }

        var call = NewCall();
        var dispatch = NewDispatch(call.Id, ambulance.Id, DispatchStatus.Assigned);

        await using (var db = _fixture.CreateContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.EmergencyCalls.Add(call);
            db.Dispatches.Add(dispatch);
            var tracked = await db.Ambulances.SingleAsync(row => row.Id == ambulance.Id);
            tracked.Status = AmbulanceStatus.Dispatched;
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var check = _fixture.CreateContext();
        Assert.False(await check.EmergencyCalls.AnyAsync(row => row.Id == call.Id));
        Assert.False(await check.Dispatches.AnyAsync(row => row.Id == dispatch.Id));
        Assert.Equal(AmbulanceStatus.Available,
            (await check.Ambulances.SingleAsync(row => row.Id == ambulance.Id)).Status);
    }

    [Fact]
    [Trait("id", "EM-DB-13")]
    public async Task Two_people_changing_the_same_run_at_once_cannot_both_win()
    {
        Guid dispatchId;
        await using (var seed = _fixture.CreateContext())
        {
            dispatchId = (await SeedRunAsync(seed, withProposal: false)).DispatchId;
        }

        await using var crewSide = _fixture.CreateContext();
        await using var managerSide = _fixture.CreateContext();
        var crewCopy = await crewSide.Dispatches.SingleAsync(row => row.Id == dispatchId);
        var managerCopy = await managerSide.Dispatches.SingleAsync(row => row.Id == dispatchId);

        crewCopy.Status = DispatchStatus.Acknowledged;
        crewCopy.AcknowledgedAt = DateTimeOffset.UtcNow;
        await crewSide.SaveChangesAsync();

        managerCopy.Status = DispatchStatus.Cancelled;
        managerCopy.CancellationReason = "QM Test";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => managerSide.SaveChangesAsync());

        await using var check = _fixture.CreateContext();
        Assert.Equal(DispatchStatus.Acknowledged, (await check.Dispatches.SingleAsync(row => row.Id == dispatchId)).Status);
    }

    [Fact]
    [Trait("id", "EM-DB-14")]
    public async Task One_call_has_at_most_one_pre_admission_notice()
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db, withProposal: false);
        db.PreAdmissionNotices.Add(NewNotice(ids.CallId, ids.DispatchId, attempts: 0));
        await db.SaveChangesAsync();

        db.PreAdmissionNotices.Add(NewNotice(ids.CallId, ids.DispatchId, attempts: 0));

        await AssertViolation(() => db.SaveChangesAsync(), UniqueViolation, "ux_pre_admission_notices_call");
    }

    [Fact]
    [Trait("id", "EM-DB-14b")]
    public async Task A_pre_admission_notice_cannot_have_a_negative_attempt_count()
    {
        await using var db = _fixture.CreateContext();
        var ids = await SeedRunAsync(db, withProposal: false);
        db.PreAdmissionNotices.Add(NewNotice(ids.CallId, ids.DispatchId, attempts: -1));

        await AssertViolation(() => db.SaveChangesAsync(), CheckViolation, "ck_pre_admission_notices_attempts");
    }

    [Fact]
    [Trait("id", "EM-DB-15")]
    public async Task Every_migration_applies_to_an_empty_database()
    {
        await using var db = _fixture.CreateContext();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var emergency = applied.Where(name => name.Contains("_Emergency_", StringComparison.Ordinal)).ToList();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations().Count(), applied.Count);
        Assert.Equal(15, emergency.Count);
        Assert.EndsWith("_Emergency_AddFoundation", emergency[0], StringComparison.Ordinal);
        Assert.EndsWith("_Emergency_CloseCallsAndRuns", emergency[^1], StringComparison.Ordinal);
    }

    private sealed record RunIds(Guid AmbulanceId, Guid CallId, Guid DispatchId, Guid ProposalId);

    private static async Task<RunIds> SeedRunAsync(CareLankaDbContext db, bool withProposal = true)
    {
        var ambulance = NewAmbulance(NewRegistration());
        var call = NewCall();
        var dispatch = NewDispatch(call.Id, ambulance.Id, DispatchStatus.Assigned);
        var proposal = NewProposal(call.Id, DispatchProposalStatus.Executed);
        db.AddRange(ambulance, call, dispatch);
        if (withProposal)
        {
            db.DispatchProposals.Add(proposal);
        }

        await db.SaveChangesAsync();
        return new RunIds(ambulance.Id, call.Id, dispatch.Id, proposal.Id);
    }

    private static async Task AssertViolation(Func<Task> action, string sqlState, string constraint)
    {
        var error = await Record.ExceptionAsync(action);
        var postgres = error as PostgresException ?? error?.InnerException as PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
    }

    private static decimal Dec(string value)
        => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static string NewRegistration() => $"QM-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static Ambulance NewAmbulance(string registration)
    {
        var now = DateTimeOffset.UtcNow;
        return new Ambulance
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = registration,
            Status = AmbulanceStatus.Available,
            CurrentLatitude = 6.9271m,
            CurrentLongitude = 79.8612m,
            LocationUpdatedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static EmergencyCall NewCall(
        Guid? idempotencyKey = null, decimal latitude = 6.9271m, decimal longitude = 79.8612m)
    {
        var now = DateTimeOffset.UtcNow;
        return new EmergencyCall
        {
            Id = Guid.NewGuid(),
            CallerName = "QM Test EM-DB",
            CallerPhone = "0770000000",
            Latitude = latitude,
            Longitude = longitude,
            LocationAccuracyMetres = 15m,
            LocationCapturedAt = now,
            IdempotencyKey = idempotencyKey ?? Guid.NewGuid(),
            Details = "QM Test call",
            Priority = CallPriority.High,
            Status = CallStatus.Received,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Dispatch NewDispatch(Guid callId, Guid ambulanceId, DispatchStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new Dispatch
        {
            Id = Guid.NewGuid(),
            EmergencyCallId = callId,
            AmbulanceId = ambulanceId,
            Status = status,
            DispatchedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static DispatchProposal NewProposal(Guid callId, DispatchProposalStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new DispatchProposal
        {
            Id = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            EmergencyCallId = callId,
            CallPriority = CallPriority.High,
            Status = status,
            AllowDiversion = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static StaffMember NewStaff(StaffRole role)
    {
        var now = DateTimeOffset.UtcNow;
        var tag = Guid.NewGuid().ToString("N")[..10];
        return new StaffMember
        {
            Id = Guid.NewGuid(),
            Email = $"qm-em-db-{tag}@carelanka.lk",
            PasswordHash = "not-a-real-hash",
            FirstName = "QM Test",
            LastName = tag,
            Role = role,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static AmbulanceCrewAssignment NewCrewAssignment(Guid ambulanceId, Guid staffId, Guid assignedBy)
    {
        var now = DateTimeOffset.UtcNow;
        return new AmbulanceCrewAssignment
        {
            Id = Guid.NewGuid(),
            AmbulanceId = ambulanceId,
            StaffMemberId = staffId,
            AssignedByStaffId = assignedBy,
            AssignedAt = now,
            CreatedAt = now
        };
    }

    private static RouteLog NewRouteLog(Guid dispatchId, decimal distanceKm, int minutes)
    {
        var now = DateTimeOffset.UtcNow;
        return new RouteLog
        {
            Id = Guid.NewGuid(),
            DispatchId = dispatchId,
            OriginLatitude = 6.9271m,
            OriginLongitude = 79.8612m,
            DestinationLatitude = 6.9000m,
            DestinationLongitude = 79.8700m,
            PlannedDistanceKm = distanceKm,
            PlannedDurationMinutes = minutes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static PreAdmissionNotice NewNotice(Guid callId, Guid dispatchId, int attempts)
    {
        var now = DateTimeOffset.UtcNow;
        return new PreAdmissionNotice
        {
            Id = Guid.NewGuid(),
            EmergencyCallId = callId,
            DispatchId = dispatchId,
            Status = PreAdmissionStatus.Queued,
            AttemptCount = attempts,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
