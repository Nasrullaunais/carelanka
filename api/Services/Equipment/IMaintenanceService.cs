using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using ScheduleEntity = CareLanka.Api.Data.Entities.Equipment.MaintenanceSchedule;

namespace CareLanka.Api.Services.Equipment;

public interface IMaintenanceService
{
    Task<PagedResult<MaintenanceSchedule>> ListAsync(
        MaintenanceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The manual path, with no agent involved. It has to keep working: if the only way to
    /// book a service were through the agent, the hospital stops the day the agent does.
    /// Scheduling against an occupied bed is refused before anything is written.
    /// </summary>
    Task<MaintenanceSchedule> CreateAsync(
        CreateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the work done and puts the asset back in service, in one transaction. Also
    /// advances the item's next service date and closes any warning that led here.
    /// </summary>
    Task<MaintenanceSchedule> CompleteAsync(
        Guid id, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such schedule. For internal lookups.</summary>
    Task<ScheduleEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such schedule.</summary>
    Task<ScheduleEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

// Overdue is not a stored status, so it cannot simply be a Status filter. Asking for
// Overdue - either as the status or through the flag - means "scheduled, and the date has
// passed", evaluated against today when the query runs.
public record MaintenanceQuery(
    MaintenanceStatus? Status = null,
    AssetType? AssetType = null,
    bool? Overdue = null,
    int Page = 1,
    int PageSize = 20);
