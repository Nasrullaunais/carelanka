using CareLanka.Api.DTOs.Equipment;
using CategoryEntity = CareLanka.Api.Data.Entities.Equipment.EquipmentCategory;

namespace CareLanka.Api.Services.Equipment;

public interface IEquipmentCategoryService
{
    Task<IReadOnlyList<EquipmentCategory>> ListAsync(CancellationToken cancellationToken = default);

    Task<EquipmentCategory> CreateAsync(
        CreateEquipmentCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such live category. For internal lookups - use GetByIdAsync to answer a request.</summary>
    Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such live category.</summary>
    Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
