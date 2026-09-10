using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using ItemEntity = CareLanka.Api.Data.Entities.Equipment.EquipmentItem;

namespace CareLanka.Api.Services.Equipment;

public interface IEquipmentItemService
{
    Task<PagedResult<EquipmentItemSummary>> ListAsync(
        EquipmentItemQuery query, CancellationToken cancellationToken = default);

    Task<EquipmentItem> CreateAsync(
        CreateEquipmentItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>The item with its servicing history and any warnings still open.</summary>
    Task<EquipmentItemDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>What a scanned QR tag resolves to. 404 when nothing carries the tag.</summary>
    Task<EquipmentItemDetail> GetDetailByTagAsync(
        string assetTag, CancellationToken cancellationToken = default);

    Task<EquipmentItem> UpdateAsync(
        Guid id, UpdateEquipmentItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>available to assigned. 409 when the item is anything else.</summary>
    Task<EquipmentItem> AssignAsync(
        Guid id, Guid admissionId, CancellationToken cancellationToken = default);

    /// <summary>assigned to available, clearing the admission. 409 when it was not assigned.</summary>
    Task<EquipmentItem> ReleaseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sets the item to maintenance and raises a warning, in one transaction.</summary>
    Task<EquipmentItem> ReportFaultAsync(
        Guid id, string description, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such live item. For internal lookups - use the detail methods to answer a request.</summary>
    Task<ItemEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such live item.</summary>
    Task<ItemEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

// Search matches name, model, manufacturer, asset tag or serial number. SortBy is one of
// name, purchase_date, next_maintenance_due or created_at; anything else falls back to
// created_at rather than failing, because a bad sort is not worth a 400.
public record EquipmentItemQuery(
    string? Search = null,
    Guid? CategoryId = null,
    Guid? WardId = null,
    EquipmentStatus? Status = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "created_at",
    string SortDir = "desc");
