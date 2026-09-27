using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Patient;
using CareLanka.Api.Services.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class NotifierTests
{
    private readonly ApiApplication _application;

    public NotifierTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_second_notify_for_the_same_type_subject_and_recipient_is_ignored()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var staff = await SeedStaffAsync(db, StaffRole.AmbulanceCrew);
        var entityId = Guid.NewGuid();

        var notifier = CreateNotifier(scope);
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Staff(staff.Id),
            new NotificationSubject("dispatch", entityId));
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Staff(staff.Id),
            new NotificationSubject("dispatch", entityId));
        await db.SaveChangesAsync();

        Assert.Single(await db.Notifications.Where(n => n.EntityId == entityId).ToListAsync());

        // A retried request lands in a fresh unit of work, after the first one already committed.
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Staff(staff.Id),
            new NotificationSubject("dispatch", entityId));
        await db.SaveChangesAsync();

        Assert.Single(await db.Notifications.Where(n => n.EntityId == entityId).ToListAsync());
    }

    [Fact]
    public async Task A_patient_with_no_login_receives_nothing_and_nothing_is_staged()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var patient = await SeedPatientAsync(db, userAccountId: null);
        var entityId = Guid.NewGuid();

        var notifier = CreateNotifier(scope);
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Patient(patient.Id),
            new NotificationSubject("appointment", entityId));
        await db.SaveChangesAsync();

        Assert.Empty(await db.Notifications.Where(n => n.EntityId == entityId).ToListAsync());
    }

    [Fact]
    public async Task A_role_on_a_ward_reaches_only_whoever_is_on_shift_there()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var wardId = Guid.NewGuid();

        var onShift = await SeedStaffAsync(db, StaffRole.DutyManager);
        var offShift = await SeedStaffAsync(db, StaffRole.DutyManager);
        var entityId = Guid.NewGuid();

        var notifier = CreateNotifier(scope, allocations: new FakeAllocationService(wardId, [onShift.Id]));
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Role(StaffRole.DutyManager, wardId),
            new NotificationSubject("ward", entityId));
        await db.SaveChangesAsync();

        var notified = await db.Notifications.Where(n => n.EntityId == entityId)
            .Select(n => n.RecipientStaffMemberId).ToListAsync();
        Assert.Equal([onShift.Id], notified);
        Assert.DoesNotContain(offShift.Id, notified);
    }

    [Fact]
    public async Task A_role_on_a_ward_falls_back_to_the_whole_role_when_nobody_is_on_shift()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var wardId = Guid.NewGuid();
        var staffA = await SeedStaffAsync(db, StaffRole.DutyManager);
        var staffB = await SeedStaffAsync(db, StaffRole.DutyManager);
        var entityId = Guid.NewGuid();

        var notifier = CreateNotifier(scope, allocations: new FakeAllocationService(wardId, []));
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Role(StaffRole.DutyManager, wardId),
            new NotificationSubject("ward", entityId));
        await db.SaveChangesAsync();

        var notified = await db.Notifications.Where(n => n.EntityId == entityId)
            .Select(n => n.RecipientStaffMemberId).ToListAsync();
        Assert.Contains(staffA.Id, notified);
        Assert.Contains(staffB.Id, notified);
    }

    [Fact]
    public async Task The_person_who_caused_the_event_is_never_notified_about_their_own_action()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var actor = await SeedStaffAsync(db, StaffRole.AmbulanceCrew);
        var entityId = Guid.NewGuid();

        var notifier = CreateNotifier(scope, actorStaffId: actor.Id);
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Staff(actor.Id),
            new NotificationSubject("dispatch", entityId));
        await db.SaveChangesAsync();

        Assert.Empty(await db.Notifications.Where(n => n.EntityId == entityId).ToListAsync());
    }

    [Fact]
    public async Task A_save_that_fails_for_an_unrelated_reason_leaves_no_notification_behind()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var staff = await SeedStaffAsync(db, StaffRole.AmbulanceCrew);
        var entityId = Guid.NewGuid();

        var notifier = CreateNotifier(scope);
        await notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Staff(staff.Id),
            new NotificationSubject("dispatch", entityId));
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), Type = NotificationType.DispatchAssigned, Title = "t", Body = "b",
            DedupeKey = $"broken:{Guid.NewGuid()}"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        using var freshScope = _application.Services.CreateScope();
        var freshDb = freshScope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.Empty(await freshDb.Notifications.Where(n => n.EntityId == entityId).ToListAsync());
    }

    private static Notifier CreateNotifier(
        IServiceScope scope, Guid? actorStaffId = null, IAllocationService? allocations = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var resolver = allocations is null
            ? scope.ServiceProvider.GetRequiredService<IRecipientResolver>()
            : new RecipientResolver(
                db, scope.ServiceProvider.GetRequiredService<IPatientService>(), allocations, TimeProvider.System);
        var currentUser = actorStaffId is { } id
            ? new FakeCurrentUser(PrincipalType.Staff, id)
            : new FakeCurrentUser();
        return new Notifier(db, resolver, currentUser, TimeProvider.System);
    }

    private static async Task<StaffMember> SeedStaffAsync(CareLankaDbContext db, StaffRole role)
    {
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(), Email = $"notifier-{Guid.NewGuid():N}@carelanka.invalid", PasswordHash = "x",
            FirstName = "Notifier", LastName = "Test", Role = role, IsActive = true
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }

    private static async Task<Patient> SeedPatientAsync(CareLankaDbContext db, Guid? userAccountId)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(), PatientCode = Guid.NewGuid().ToString("N")[..8], FullName = "Test Patient",
            Gender = Gender.Female, Phone = "0770000000", UserAccountId = userAccountId
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    // Staff's shift/allocation tables have no migration yet (a pre-existing gap, unrelated to
    // notifications), so on-shift resolution is exercised through a fake rather than real rows.
    private sealed class FakeAllocationService(Guid wardId, IReadOnlyCollection<Guid> onShiftStaffIds) : IAllocationService
    {
        public Task<IReadOnlyCollection<Guid>> FindOnShiftAsync(
            Guid queriedWardId, StaffRole role, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.FromResult(queriedWardId == wardId ? onShiftStaffIds : []);

        public Task<PagedResult<AllocationDto>> ListAllocationsAsync(
            ListAllocationsQueryParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AllocationDto> CreateAllocationAsync(
            CreateAllocationRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EndAllocationResponse> EndAllocationAsync(
            Guid id, EndAllocationRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser() { }

        public FakeCurrentUser(PrincipalType principalType, Guid id)
        {
            IsAuthenticated = true;
            PrincipalType = principalType;
            Id = id;
        }

        public bool IsAuthenticated { get; }
        public Guid Id { get; }
        public PrincipalType PrincipalType { get; }
        public PrincipalRole Role => PrincipalRole.GeneralStaff;
    }
}
