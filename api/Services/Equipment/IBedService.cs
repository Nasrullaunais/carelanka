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

    /// <summary>Rejected with 409 when the change would withdraw a bed Patient Management reports occupied or held.</summary>
    Task<Bed> UpdateAsync(Guid id, UpdateBedRequest request, CancellationToken cancellationToken = default);

    /// <summary>Irreversible. Same occupancy check as an update that withdraws the bed.</summary>
    Task<Bed> RetireAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such live bed. For internal lookups - use GetByIdAsync to answer a request.</summary>
    Task<BedEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such live bed.</summary>
    Task<BedEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many beds each of these wards has. A ward with no beds is absent from the result,
    /// not zero. Signature deliberately identical to Patient Management's IBedRegistryService,
    /// so replacing their stub is a delegating adapter and a DI line - see STUBS.md row 1.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountBedsByWardAsync(
        IReadOnlyCollection<Guid> wardIds, CancellationToken cancellationToken = default);
}
