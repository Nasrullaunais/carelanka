using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using BedEntity = CareLanka.Api.Data.Entities.Equipment.Bed;

namespace CareLanka.Api.Services.Equipment;

public interface IBedService
{
    Task<PagedResult<Bed>> ListAsync(
        Guid? wardId, BedCondition? condition, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<Bed> CreateAsync(CreateBedRequest request, CancellationToken cancellationToken = default);

    Task<Bed> UpdateAsync(Guid id, UpdateBedRequest request, CancellationToken cancellationToken = default);

    Task<Bed> RetireAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BedEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BedEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);
}
