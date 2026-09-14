using CareLanka.Api.DTOs.Equipment;
using CategoryEntity = CareLanka.Api.Data.Entities.Equipment.EquipmentCategory;

namespace CareLanka.Api.Services.Equipment;

public interface IEquipmentCategoryService
{
    Task<IReadOnlyList<EquipmentCategory>> ListAsync(CancellationToken cancellationToken = default);

    Task<EquipmentCategory> CreateAsync(
        CreateEquipmentCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
