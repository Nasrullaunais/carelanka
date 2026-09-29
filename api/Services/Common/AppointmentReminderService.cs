using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Equipment;
using CareLanka.Api.Services.Patient;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

public interface IAppointmentReminderService
{
    Task<int> SendDueAsync(CancellationToken cancellationToken = default);
}

public sealed class AppointmentReminderService(
    IAppointmentService appointments,
    INotifier notifier,
    CareLankaDbContext db,
    TimeProvider clock,
    IOptions<NotificationOptions> options) : IAppointmentReminderService
{
    // Looks back this far so a reminder is still sent after a short outage. Appointments booked
    // closer than the lead time were just confirmed to the patient, so they get no reminder.
    private static readonly TimeSpan CatchUp = TimeSpan.FromHours(1);

    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var target = clock.GetUtcNow().AddHours(options.Value.ReminderLeadHours);
        var due = await appointments.ListOpenStartingBetweenAsync(target - CatchUp, target, cancellationToken);

        foreach (var appointment in due)
        {
            var localTime = TimeZoneInfo.ConvertTime(appointment.ScheduledAt, HospitalTime.Zone);
            await notifier.NotifyAsync(
                NotificationType.AppointmentReminder,
                Recipients.Patient(appointment.PatientId),
                new NotificationSubject("appointment", appointment.Id),
                cancellationToken,
                localTime);
        }

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
