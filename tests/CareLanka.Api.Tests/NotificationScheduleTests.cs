using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class NotificationScheduleTests
{
    private static readonly DateTimeOffset Now = new(2030, 6, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly ApiApplication _application;

    public NotificationScheduleTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_reminder_goes_out_for_an_appointment_a_day_away_and_not_a_second_time()
    {
        var (patientAccountId, appointmentId) = await SeedAppointmentAsync(Now.AddHours(24));

        await RemindAsync(Now);
        await RemindAsync(Now.AddMinutes(15));

        var reminders = await NotificationsAboutAsync(appointmentId);
        var reminder = Assert.Single(reminders);
        Assert.Equal(patientAccountId, reminder.RecipientPatientAccountId);
        Assert.Equal(NotificationType.AppointmentReminder, reminder.Type);
    }

    [Fact]
    public async Task No_reminder_goes_out_for_an_appointment_that_is_still_far_away()
    {
        var (_, appointmentId) = await SeedAppointmentAsync(Now.AddHours(24).AddMinutes(30));

        await RemindAsync(Now);

        Assert.Empty(await NotificationsAboutAsync(appointmentId));
    }

    [Fact]
    public async Task No_reminder_goes_out_for_an_appointment_already_inside_the_last_day()
    {
        var (_, appointmentId) = await SeedAppointmentAsync(Now.AddHours(20));

        await RemindAsync(Now);

        Assert.Empty(await NotificationsAboutAsync(appointmentId));
    }

    [Fact]
    public async Task No_reminder_goes_out_for_an_appointment_that_is_cancelled_or_finished()
    {
        var (_, cancelledId) = await SeedAppointmentAsync(Now.AddHours(24), AppointmentStatus.Cancelled);
        var (_, completedId) = await SeedAppointmentAsync(Now.AddHours(24), AppointmentStatus.Completed);

        await RemindAsync(Now);

        Assert.Empty(await NotificationsAboutAsync(cancelledId));
        Assert.Empty(await NotificationsAboutAsync(completedId));
    }

    [Fact]
    public async Task A_missed_run_is_caught_up_within_the_hour_but_not_later()
    {
        var (_, caughtUpId) = await SeedAppointmentAsync(Now.AddHours(23).AddMinutes(30));
        var (_, tooLateId) = await SeedAppointmentAsync(Now.AddHours(22));

        await RemindAsync(Now);

        Assert.Single(await NotificationsAboutAsync(caughtUpId));
        Assert.Empty(await NotificationsAboutAsync(tooLateId));
    }

    [Fact]
    public async Task The_reminder_lead_time_comes_from_the_settings()
    {
        var (_, appointmentId) = await SeedAppointmentAsync(Now.AddHours(48));

        await RemindAsync(Now, new NotificationOptions { ReminderLeadHours = 48 });

        Assert.Single(await NotificationsAboutAsync(appointmentId));
    }

    [Fact]
    public async Task Maintenance_due_today_in_sri_lanka_alerts_every_equipment_manager_once()
    {
        var manager = await SeedStaffAsync(StaffRole.EquipmentManager);
        var otherRole = await SeedStaffAsync(StaffRole.DutyManager);
        var scheduleId = await SeedScheduleAsync(new DateOnly(2030, 6, 10));

        // 19:00 UTC on 9 June is already 00:30 on 10 June in Sri Lanka.
        var justPastLocalMidnight = new DateTimeOffset(2030, 6, 9, 19, 0, 0, TimeSpan.Zero);
        await AlertAsync(justPastLocalMidnight);
        await AlertAsync(justPastLocalMidnight.AddHours(1));

        var alerts = await NotificationsAboutAsync(scheduleId);
        Assert.Contains(alerts, n => n.RecipientStaffMemberId == manager.Id);
        Assert.DoesNotContain(alerts, n => n.RecipientStaffMemberId == otherRole.Id);
        Assert.Equal(alerts.Count, alerts.Select(n => n.RecipientStaffMemberId).Distinct().Count());
        Assert.All(alerts, n => Assert.Equal(NotificationType.MaintenanceDue, n.Type));
    }

    [Fact]
    public async Task Maintenance_is_not_due_until_the_day_starts_in_sri_lanka()
    {
        await SeedStaffAsync(StaffRole.EquipmentManager);
        var scheduleId = await SeedScheduleAsync(new DateOnly(2030, 6, 10));

        // 18:00 UTC on 9 June is 23:30 on 9 June in Sri Lanka: still yesterday there.
        await AlertAsync(new DateTimeOffset(2030, 6, 9, 18, 0, 0, TimeSpan.Zero));

        Assert.Empty(await NotificationsAboutAsync(scheduleId));
    }

    [Fact]
    public async Task Maintenance_that_is_finished_or_cancelled_is_not_announced()
    {
        await SeedStaffAsync(StaffRole.EquipmentManager);
        var completedId = await SeedScheduleAsync(new DateOnly(2030, 6, 10), MaintenanceStatus.Completed);
        var cancelledId = await SeedScheduleAsync(new DateOnly(2030, 6, 10), MaintenanceStatus.Cancelled);

        await AlertAsync(new DateTimeOffset(2030, 6, 10, 6, 0, 0, TimeSpan.Zero));

        Assert.Empty(await NotificationsAboutAsync(completedId));
        Assert.Empty(await NotificationsAboutAsync(cancelledId));
    }

    [Fact]
    public async Task Clean_up_deletes_only_notifications_older_than_the_retention_period_with_their_deliveries()
    {
        var staff = await SeedStaffAsync(StaffRole.EquipmentManager);
        var old = await SeedNotificationAsync(staff.Id, ageInDays: 91);
        var borderline = await SeedNotificationAsync(staff.Id, ageInDays: 89);
        var fresh = await SeedNotificationAsync(staff.Id, ageInDays: 0);

        using var scope = _application.Services.CreateScope();
        var cleanup = new InboxCleanupService(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            new FixedClock(DateTimeOffset.UtcNow),
            Options.Create(new NotificationOptions { RetentionDays = 90 }));
        await cleanup.DeleteExpiredAsync();

        using var check = _application.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.False(await db.Notifications.AnyAsync(n => n.Id == old));
        Assert.False(await db.NotificationDeliveries.AnyAsync(d => d.NotificationId == old));
        Assert.True(await db.Notifications.AnyAsync(n => n.Id == borderline));
        Assert.True(await db.Notifications.AnyAsync(n => n.Id == fresh));
        Assert.True(await db.NotificationDeliveries.AnyAsync(d => d.NotificationId == fresh));
    }

    [Fact]
    public async Task Clean_up_uses_the_retention_period_from_the_settings()
    {
        var staff = await SeedStaffAsync(StaffRole.EquipmentManager);
        var tenDaysOld = await SeedNotificationAsync(staff.Id, ageInDays: 10);

        using var scope = _application.Services.CreateScope();
        var cleanup = new InboxCleanupService(
            scope.ServiceProvider.GetRequiredService<CareLankaDbContext>(),
            new FixedClock(DateTimeOffset.UtcNow),
            Options.Create(new NotificationOptions { RetentionDays = 7 }));
        await cleanup.DeleteExpiredAsync();

        using var check = _application.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Notifications.AnyAsync(n => n.Id == tenDaysOld));
    }

    [Fact]
    public void The_real_app_runs_the_schedule_worker_and_has_the_default_settings()
    {
        var workers = _application.Services.GetServices<IHostedService>();
        Assert.Contains(workers, worker => worker is NotificationScheduleWorker);

        var options = _application.Services.GetRequiredService<IOptions<NotificationOptions>>().Value;
        Assert.Equal(90, options.RetentionDays);
        Assert.Equal(24, options.ReminderLeadHours);
    }

    private async Task RemindAsync(DateTimeOffset now, NotificationOptions? options = null)
    {
        using var scope = _application.Services.CreateScope();
        var services = scope.ServiceProvider;
        var reminders = new AppointmentReminderService(
            services.GetRequiredService<CareLanka.Api.Services.Patient.IAppointmentService>(),
            services.GetRequiredService<INotifier>(),
            services.GetRequiredService<CareLankaDbContext>(),
            new FixedClock(now),
            Options.Create(options ?? new NotificationOptions()));
        await reminders.SendDueAsync();
    }

    private async Task AlertAsync(DateTimeOffset now)
    {
        using var scope = _application.Services.CreateScope();
        var services = scope.ServiceProvider;
        var alerts = new MaintenanceDueService(
            services.GetRequiredService<CareLanka.Api.Services.Equipment.IMaintenanceService>(),
            services.GetRequiredService<INotifier>(),
            services.GetRequiredService<CareLankaDbContext>(),
            new FixedClock(now));
        await alerts.SendDueAsync();
    }

    private async Task<List<Notification>> NotificationsAboutAsync(Guid entityId)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .Notifications.AsNoTracking().Where(n => n.EntityId == entityId).ToListAsync();
    }

    private async Task<(Guid PatientAccountId, Guid AppointmentId)> SeedAppointmentAsync(
        DateTimeOffset scheduledAt, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var account = new PatientAccount
        {
            Id = Guid.NewGuid(), Username = $"reminder-{Guid.NewGuid():N}", PasswordHash = "x"
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(), PatientCode = Guid.NewGuid().ToString("N")[..8], FullName = "Reminder Patient",
            Gender = Gender.Female, Phone = "0770000000", UserAccountId = account.Id
        };
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, ScheduledAt = scheduledAt, Status = status
        };
        db.PatientAccounts.Add(account);
        db.Patients.Add(patient);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return (account.Id, appointment.Id);
    }

    private async Task<StaffMember> SeedStaffAsync(StaffRole role)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(), Email = $"schedule-{Guid.NewGuid():N}@carelanka.invalid", PasswordHash = "x",
            FirstName = "Schedule", LastName = "Test", Role = role, IsActive = true
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }

    private async Task<Guid> SeedScheduleAsync(DateOnly date, MaintenanceStatus status = MaintenanceStatus.Scheduled)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(), AssetType = AssetType.EquipmentItem, AssetId = Guid.NewGuid(),
            ScheduleType = MaintenanceType.RoutineService, ScheduledDate = date, Status = status,
            CreatedBy = RaisedBy.User
        };
        db.MaintenanceSchedules.Add(schedule);
        await db.SaveChangesAsync();
        return schedule.Id;
    }

    private async Task<Guid> SeedNotificationAsync(Guid staffId, int ageInDays)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var notification = new Notification
        {
            Id = Guid.NewGuid(), RecipientStaffMemberId = staffId, Type = NotificationType.MaintenanceDue,
            Title = "t", Body = "b", DedupeKey = $"cleanup:{Guid.NewGuid()}"
        };
        db.Notifications.Add(notification);
        db.NotificationDeliveries.Add(new NotificationDelivery
        {
            Id = Guid.NewGuid(), NotificationId = notification.Id, Notification = notification,
            Channel = NotificationChannel.Push, Status = NotificationStatus.Sent,
            NextAttemptAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // CreatedAt is stamped by the database layer on insert, so age it afterwards.
        var createdAt = DateTimeOffset.UtcNow.AddDays(-ageInDays);
        await db.Notifications.Where(n => n.Id == notification.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, createdAt));
        return notification.Id;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
