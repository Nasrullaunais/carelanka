using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

public interface IWardService
{
    Task<IReadOnlyList<Ward>> ListAsync(
        WardType? wardType, bool isActive, CancellationToken cancellationToken = default);

    Task<Ward> CreateAsync(CreateWardRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such active ward. For internal lookups — use GetByIdAsync to answer a request.</summary>
    Task<WardEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such active ward.</summary>
    Task<WardEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
