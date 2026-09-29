using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Equipment;

namespace CareLanka.Api.Services.Common;

public interface IMaintenanceDueService
{
    Task<int> SendDueAsync(CancellationToken cancellationToken = default);
}

// One notification per schedule: the dedupe key is the schedule, so the hourly run never repeats it.
public sealed class MaintenanceDueService(
    IMaintenanceService maintenance,
    INotifier notifier,
    CareLankaDbContext db,
    TimeProvider clock) : IMaintenanceDueService
{
    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var today = HospitalTime.Today(clock.GetUtcNow());
        var due = await maintenance.ListScheduledOnAsync(today, cancellationToken);

        foreach (var schedule in due)
        {
            await notifier.NotifyAsync(
                NotificationType.MaintenanceDue,
                Recipients.Role(StaffRole.EquipmentManager),
                new NotificationSubject("maintenance_schedule", schedule.Id),
                cancellationToken,
                1);
        }

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
