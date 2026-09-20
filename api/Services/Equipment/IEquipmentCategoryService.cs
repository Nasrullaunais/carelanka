using CareLanka.Api.DTOs.Equipment;
using CategoryEntity = CareLanka.Api.Data.Entities.Equipment.EquipmentCategory;

namespace CareLanka.Api.Services.Equipment;

public interface IEquipmentCategoryService
{
    Task<IReadOnlyList<EquipmentCategory>> ListAsync(CancellationToken cancellationToken = default);

    Task<EquipmentCategory> CreateAsync(
        CreateEquipmentCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every category with how many items use it, for the administrator to tidy.</summary>
    Task<IReadOnlyList<EquipmentCategoryUsage>> ListForRemovalAsync(
        string? confirmationCode, CancellationToken cancellationToken = default);

    /// <summary>Takes an unused category off the list. Refused while any item uses it.</summary>
    Task RemoveAsync(Guid id, string? confirmationCode, CancellationToken cancellationToken = default);

    Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
