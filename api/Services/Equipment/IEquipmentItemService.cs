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

    Task<EquipmentItemDetail> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EquipmentItemDetail> GetDetailByTagAsync(
        string assetTag, CancellationToken cancellationToken = default);

    Task<EquipmentItem> UpdateAsync(
        Guid id, UpdateEquipmentItemRequest request, CancellationToken cancellationToken = default);

    Task<EquipmentItem> AssignAsync(
        Guid id, Guid admissionId, CancellationToken cancellationToken = default);

    Task<EquipmentItem> ReleaseAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EquipmentItem> ReportFaultAsync(
        Guid id, string description, CancellationToken cancellationToken = default);

    Task<ItemEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ItemEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public record EquipmentItemQuery(
    string? Search = null,
    Guid? CategoryId = null,
    Guid? WardId = null,
    EquipmentStatus? Status = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "created_at",
    string SortDir = "desc");
