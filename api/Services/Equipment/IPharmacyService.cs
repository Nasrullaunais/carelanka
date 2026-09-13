using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CategoryEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyCategory;
using ItemEntity = CareLanka.Api.Data.Entities.Equipment.PharmacyItem;

namespace CareLanka.Api.Services.Equipment;

public interface IPharmacyCategoryService
{
    Task<IReadOnlyList<PharmacyCategory>> ListAsync(CancellationToken cancellationToken = default);

    Task<PharmacyCategory> CreateAsync(
        CreatePharmacyCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPharmacyItemService
{
    Task<PagedResult<PharmacyItem>> ListAsync(
        PharmacyItemQuery query, CancellationToken cancellationToken = default);

    Task<PharmacyItem> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PharmacyItem> CreateAsync(
        CreatePharmacyItemRequest request, CancellationToken cancellationToken = default);

    Task<PharmacyItem> RecordTransactionAsync(
        Guid id, CreatePharmacyTransactionRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<PharmacyTransaction>> ListTransactionsAsync(
        Guid id, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ItemEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ItemEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public record PharmacyItemQuery(
    string? Search = null,
    Guid? CategoryId = null,
    bool AvailableOnly = false,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "name",
    string SortDir = "asc");
