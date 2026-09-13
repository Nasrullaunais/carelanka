using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using ScheduleEntity = CareLanka.Api.Data.Entities.Equipment.MaintenanceSchedule;

namespace CareLanka.Api.Services.Equipment;

public interface IMaintenanceService
{
    Task<PagedResult<MaintenanceSchedule>> ListAsync(
        MaintenanceQuery query, CancellationToken cancellationToken = default);

    Task<MaintenanceSchedule> CreateAsync(
        CreateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default);

    Task<MaintenanceSchedule> CompleteAsync(
        Guid id, string? notes, CancellationToken cancellationToken = default);

    Task<ScheduleEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ScheduleEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public record MaintenanceQuery(
    MaintenanceStatus? Status = null,
    AssetType? AssetType = null,
    bool? Overdue = null,
    int Page = 1,
    int PageSize = 20);
