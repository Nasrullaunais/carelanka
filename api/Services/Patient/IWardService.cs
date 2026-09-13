using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

public interface IWardService
{
    Task<IReadOnlyList<Ward>> ListAsync(
        WardType? wardType, bool isActive, CancellationToken cancellationToken = default);

    Task<Ward> CreateAsync(CreateWardRequest request, CancellationToken cancellationToken = default);

    Task<WardEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WardEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
