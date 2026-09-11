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

    /// <summary>Null when there is no such live category. For internal lookups - use GetByIdAsync to answer a request.</summary>
    Task<CategoryEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such live category.</summary>
    Task<CategoryEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPharmacyItemService
{
    /// <summary>Search and availability. Open to any staff member - the literal requirement of the component plan.</summary>
    Task<PagedResult<PharmacyItem>> ListAsync(
        PharmacyItemQuery query, CancellationToken cancellationToken = default);

    Task<PharmacyItem> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PharmacyItem> CreateAsync(
        CreatePharmacyItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The only way quantity on hand ever changes. Applied as one conditional update, so
    /// stock cannot go negative and two people dispensing at once cannot both succeed past
    /// zero. Returns the item as it stands after the movement.
    /// </summary>
    Task<PharmacyItem> RecordTransactionAsync(
        Guid id, CreatePharmacyTransactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>One item's movement history, newest first.</summary>
    Task<PagedResult<PharmacyTransaction>> ListTransactionsAsync(
        Guid id, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such live item. For internal lookups - use GetAsync to answer a request.</summary>
    Task<ItemEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such live item.</summary>
    Task<ItemEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

// Search matches name or manufacturer. AvailableOnly filters to quantity_on_hand > 0, which
// is the "is it available" half of the plan's search requirement. SortBy is one of name,
// expiry_date, quantity_on_hand or created_at; anything else falls back to name rather than
// failing, because a bad sort is not worth a 400.
public record PharmacyItemQuery(
    string? Search = null,
    Guid? CategoryId = null,
    bool AvailableOnly = false,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "name",
    string SortDir = "asc");
