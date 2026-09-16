using System.Security.Cryptography;
using System.Text;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ItemEntity = CareLanka.Api.Data.Entities.Equipment.EquipmentItem;
using ScheduleEntity = CareLanka.Api.Data.Entities.Equipment.MaintenanceSchedule;
using WarningEntity = CareLanka.Api.Data.Entities.Equipment.Warning;

namespace CareLanka.Api.Services.Equipment;

public sealed class EquipmentItemService : IEquipmentItemService
{
    private readonly CareLankaDbContext _db;
    private readonly IWardDirectory _wards;
    private readonly ICurrentUser _currentUser;
    private readonly IOptions<EquipmentOptions> _options;

    public EquipmentItemService(
        CareLankaDbContext db,
        IWardDirectory wards,
        ICurrentUser currentUser,
        IOptions<EquipmentOptions> options)
    {
        _db = db;
        _wards = wards;
        _currentUser = currentUser;
        _options = options;
    }

    public async Task<PagedResult<EquipmentItemSummary>> ListAsync(
        EquipmentItemQuery query, CancellationToken cancellationToken = default)
    {
        var items = _db.EquipmentItems.AsNoTracking()
            .Include(i => i.Category)
            .Where(i => !i.AwaitingConfirmation);

        if (query.CategoryId is { } category)
        {
            items = items.Where(i => i.CategoryId == category);
        }

        if (query.WardId is { } ward)
        {
            items = items.Where(i => i.WardId == ward);
        }

        if (query.Status is { } status)
        {
            items = items.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";

            items = items.Where(i =>
                EF.Functions.ILike(i.Name, term)
                || EF.Functions.ILike(i.Model, term)
                || EF.Functions.ILike(i.Manufacturer, term)
                || EF.Functions.ILike(i.AssetTag, term)
                || (i.SerialNumber != null && EF.Functions.ILike(i.SerialNumber, term)));
        }

        var totalItems = await items.CountAsync(cancellationToken);

        var rows = await Sort(items, query.SortBy, query.SortDir)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var names = await WardNamesAsync(rows.Select(i => i.WardId), cancellationToken);

        return PagedResult<EquipmentItemSummary>.From(
            rows.Select(i => ToSummary(i, names)).ToList(),
            query.Page, query.PageSize, totalItems);
    }

    public async Task<EquipmentItem> CreateAsync(
        CreateEquipmentItemRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _db.EquipmentCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Equipment category", request.CategoryId);

        var assetTag = request.AssetTag.Trim();
        var serialNumber = Normalise(request.SerialNumber);

        if (assetTag.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        await EnsureAssetTagFreeAsync(assetTag, null, cancellationToken);
        await EnsureSerialNumberFreeAsync(serialNumber, null, cancellationToken);

        var item = new ItemEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CategoryId = category.Id,
            Category = category,
            Model = request.Model.Trim(),
            Manufacturer = request.Manufacturer.Trim(),
            PurchaseDate = request.PurchaseDate,
            Status = EquipmentStatus.Available,
            AwaitingConfirmation = true,
            WardId = request.WardId,
            AssetTag = assetTag,
            SerialNumber = serialNumber,
            NextMaintenanceDue = request.NextMaintenanceDue
        };

        _db.EquipmentItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToItemAsync(item, cancellationToken);
    }

    public async Task<EquipmentItemDetail> GetDetailAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await ToDetailAsync(await GetByIdAsync(id, cancellationToken), cancellationToken);

    public async Task<EquipmentItemDetail> GetDetailByTagAsync(
        string assetTag, CancellationToken cancellationToken = default)
    {
        var tag = assetTag.Trim();

        var item = await _db.EquipmentItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.AssetTag == tag, cancellationToken)
            ?? throw new NotFoundException("Equipment item with asset tag", tag);

        return await ToDetailAsync(item, cancellationToken);
    }

    public async Task<EquipmentItem> UpdateAsync(
        Guid id, UpdateEquipmentItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await GetConfirmedAsync(id, cancellationToken);

        if (request.Status is { } status && status != item.Status)
        {
            if (status == EquipmentStatus.Assigned)
            {
                throw new BadRequestException(MessageCode.ValidationFailed);
            }

            EnsureTransitionAllowed(item, status, TransitionReason.Update);

            if (status != EquipmentStatus.Assigned)
            {
                item.AssignedToAdmissionId = null;
            }

            item.Status = status;

            if (status == EquipmentStatus.Retired)
            {
                await CloseOpenRepairWorkAsync(item.Id, cancellationToken);
            }
        }

        if (request.Name is { } name)
        {
            item.Name = name.Trim();
        }

        if (request.Model is { } model)
        {
            item.Model = model.Trim();
        }

        if (request.Manufacturer is { } manufacturer)
        {
            item.Manufacturer = manufacturer.Trim();
        }

        if (request.WardIdSupplied)
        {
            item.WardId = request.WardId;
        }

        if (request.NextMaintenanceDueSupplied)
        {
            item.NextMaintenanceDue = request.NextMaintenanceDue;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ToItemAsync(item, cancellationToken);
    }

    public async Task<EquipmentItem> AssignAsync(
        Guid id, Guid admissionId, CancellationToken cancellationToken = default)
    {
        var item = await GetConfirmedAsync(id, cancellationToken);

        EnsureTransitionAllowed(item, EquipmentStatus.Assigned, TransitionReason.Assign);

        item.Status = EquipmentStatus.Assigned;
        item.AssignedToAdmissionId = admissionId;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToItemAsync(item, cancellationToken);
    }

    public async Task<EquipmentItem> ReleaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await GetConfirmedAsync(id, cancellationToken);

        EnsureTransitionAllowed(item, EquipmentStatus.Available, TransitionReason.Release);

        item.Status = EquipmentStatus.Available;

        item.AssignedToAdmissionId = null;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToItemAsync(item, cancellationToken);
    }

    public async Task<EquipmentItem> ReportFaultAsync(
        Guid id, string description, CancellationToken cancellationToken = default)
    {
        var item = await GetConfirmedAsync(id, cancellationToken);

        EnsureTransitionAllowed(item, EquipmentStatus.Maintenance, TransitionReason.Fault);

        item.Status = EquipmentStatus.Maintenance;

        item.AssignedToAdmissionId = null;

        _db.Warnings.Add(new WarningEntity
        {
            Id = Guid.NewGuid(),
            Type = WarningType.EquipmentFaulty,
            Severity = WarningSeverity.High,
            RelatedEntityType = RelatedEntityType.EquipmentItem,
            RelatedEntityId = item.Id,
            WardId = item.WardId,
            RecommendedAction = description.Trim(),
            Status = WarningStatus.Open,
            RaisedBy = RaisedBy.User
        });

        await OpenRepairJobAsync(item, description.Trim(), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await ToItemAsync(item, cancellationToken);
    }

    public async Task<IReadOnlyList<EquipmentItem>> ListAwaitingConfirmationAsync(
        string? confirmationCode, CancellationToken cancellationToken = default)
    {
        EnsureConfirmationCode(confirmationCode);

        var rows = await _db.EquipmentItems.AsNoTracking()
            .Include(i => i.Category)
            .Where(i => i.AwaitingConfirmation)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var names = await WardNamesAsync(rows.Select(i => i.WardId), cancellationToken);

        return rows.Select(row =>
        {
            var item = new EquipmentItem();
            Fill(item, row, names);
            return item;
        }).ToList();
    }

    public async Task<PendingEquipmentCount> CountAwaitingConfirmationAsync(
        CancellationToken cancellationToken = default)
        => new()
        {
            Count = await _db.EquipmentItems.CountAsync(i => i.AwaitingConfirmation, cancellationToken)
        };

    public async Task<EquipmentItem> ConfirmAsync(
        Guid id, string? confirmationCode, CancellationToken cancellationToken = default)
    {
        EnsureConfirmationCode(confirmationCode);

        var item = await GetAwaitingConfirmationAsync(id, cancellationToken);

        item.AwaitingConfirmation = false;
        item.ConfirmedByStaffId = _currentUser.Id;
        item.ConfirmedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToItemAsync(item, cancellationToken);
    }

    public async Task RejectAsync(
        Guid id, string? confirmationCode, CancellationToken cancellationToken = default)
    {
        EnsureConfirmationCode(confirmationCode);

        var item = await GetAwaitingConfirmationAsync(id, cancellationToken);

        item.IsActive = false;
        item.DeletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureConfirmationCode(string? supplied)
    {
        var expected = Encoding.UTF8.GetBytes(_options.Value.ConfirmationCode);
        var actual = Encoding.UTF8.GetBytes(supplied ?? string.Empty);

        if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new ForbiddenException(MessageCode.ConfirmationCodeIncorrect);
        }
    }

    private async Task<ItemEntity> GetAwaitingConfirmationAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await GetByIdAsync(id, cancellationToken);

        if (!item.AwaitingConfirmation)
        {
            throw new ConflictException(MessageCode.EquipmentNotAwaitingConfirmation, item.Name);
        }

        return item;
    }

    private async Task<ItemEntity> GetConfirmedAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await GetByIdAsync(id, cancellationToken);

        if (item.AwaitingConfirmation)
        {
            throw new ConflictException(MessageCode.EquipmentAwaitingConfirmation, item.Name);
        }

        return item;
    }

    private async Task CloseOpenRepairWorkAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var jobs = await _db.MaintenanceSchedules
            .Where(s => s.AssetType == AssetType.EquipmentItem
                        && s.AssetId == itemId
                        && (s.Status == MaintenanceStatus.Scheduled
                            || s.Status == MaintenanceStatus.InProgress))
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            job.Status = MaintenanceStatus.Cancelled;
        }

        var warnings = await _db.Warnings
            .Where(w => w.RelatedEntityType == RelatedEntityType.EquipmentItem
                        && w.RelatedEntityId == itemId
                        && w.Status == WarningStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var warning in warnings)
        {
            warning.Status = WarningStatus.ActionTaken;
            warning.ResolvedAt = now;
        }
    }

    private async Task OpenRepairJobAsync(
        ItemEntity item, string description, CancellationToken cancellationToken)
    {
        var alreadyOpen = await _db.MaintenanceSchedules.AnyAsync(
            s => s.AssetType == AssetType.EquipmentItem
                 && s.AssetId == item.Id
                 && (s.Status == MaintenanceStatus.Scheduled
                     || s.Status == MaintenanceStatus.InProgress),
            cancellationToken);

        if (alreadyOpen)
        {
            return;
        }

        _db.MaintenanceSchedules.Add(new ScheduleEntity
        {
            Id = Guid.NewGuid(),
            AssetType = AssetType.EquipmentItem,
            AssetId = item.Id,
            ScheduleType = MaintenanceType.Repair,
            ScheduledDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime),
            Status = MaintenanceStatus.Scheduled,
            Notes = description,
            CreatedBy = RaisedBy.User
        });
    }

    public Task<ItemEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.EquipmentItems.Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<ItemEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Equipment item", id);

    private enum TransitionReason
    {
        Update,
        Assign,
        Release,
        Fault
    }

    private static void EnsureTransitionAllowed(
        ItemEntity item, EquipmentStatus to, TransitionReason reason)
    {
        if (reason == TransitionReason.Fault && item.Status != EquipmentStatus.Retired)
        {
            return;
        }

        if (reason == TransitionReason.Update
            && item.Status == EquipmentStatus.Maintenance
            && to == EquipmentStatus.Available)
        {
            throw new ConflictException(MessageCode.EquipmentAwaitingRepair, item.Name);
        }

        var allowed = item.Status switch
        {
            EquipmentStatus.Available =>
                to is EquipmentStatus.Assigned or EquipmentStatus.Maintenance or EquipmentStatus.Retired,
            EquipmentStatus.Assigned => to is EquipmentStatus.Available,
            EquipmentStatus.Maintenance => to is EquipmentStatus.Available or EquipmentStatus.Retired,
            EquipmentStatus.Retired => false,
            _ => false
        };

        if (allowed)
        {
            return;
        }

        throw reason switch
        {
            TransitionReason.Release => new ConflictException(MessageCode.EquipmentNotAssigned, item.Name),
            TransitionReason.Assign or TransitionReason.Fault => new ConflictException(
                MessageCode.EquipmentNotAvailable, item.Name, EnumWire.ToWire(item.Status)),
            _ => new IllegalTransitionException(
                "Equipment item", EnumWire.ToWire(item.Status), EnumWire.ToWire(to))
        };
    }

    private async Task EnsureAssetTagFreeAsync(
        string assetTag, Guid? ignoring, CancellationToken cancellationToken)
    {
        var takenByItem = await _db.EquipmentItems.AnyAsync(
            i => i.AssetTag == assetTag && (ignoring == null || i.Id != ignoring), cancellationToken);

        var takenByBed = await _db.Beds.AnyAsync(b => b.AssetTag == assetTag, cancellationToken);

        if (takenByItem || takenByBed)
        {
            throw new ConflictException(MessageCode.AssetTagTaken, assetTag);
        }
    }

    private async Task EnsureSerialNumberFreeAsync(
        string? serialNumber, Guid? ignoring, CancellationToken cancellationToken)
    {
        if (serialNumber is null)
        {
            return;
        }

        var taken = await _db.EquipmentItems.AnyAsync(
            i => i.SerialNumber == serialNumber && (ignoring == null || i.Id != ignoring),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException(MessageCode.SerialNumberTaken, serialNumber);
        }
    }

    private static IQueryable<ItemEntity> Sort(IQueryable<ItemEntity> items, string sortBy, string sortDir)
    {
        var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortBy, descending) switch
        {
            ("name", true) => items.OrderByDescending(i => i.Name).ThenBy(i => i.Id),
            ("name", false) => items.OrderBy(i => i.Name).ThenBy(i => i.Id),
            ("purchase_date", true) => items.OrderByDescending(i => i.PurchaseDate).ThenBy(i => i.Id),
            ("purchase_date", false) => items.OrderBy(i => i.PurchaseDate).ThenBy(i => i.Id),
            ("next_maintenance_due", true) =>
                items.OrderByDescending(i => i.NextMaintenanceDue).ThenBy(i => i.Id),
            ("next_maintenance_due", false) =>
                items.OrderBy(i => i.NextMaintenanceDue).ThenBy(i => i.Id),
            (_, false) => items.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id),
            _ => items.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id)
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> WardNamesAsync(
        IEnumerable<Guid?> wardIds, CancellationToken cancellationToken)
    {
        var ids = wardIds.OfType<Guid>().Distinct().ToList();

        return ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _wards.GetWardNamesAsync(ids, cancellationToken);
    }

    private async Task<EquipmentItem> ToItemAsync(ItemEntity item, CancellationToken cancellationToken)
    {
        var names = await WardNamesAsync(new[] { item.WardId }, cancellationToken);
        var dto = new EquipmentItem();
        Fill(dto, item, names);
        return dto;
    }

    private async Task<EquipmentItemDetail> ToDetailAsync(
        ItemEntity item, CancellationToken cancellationToken)
    {
        var names = await WardNamesAsync(new[] { item.WardId }, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var history = await _db.MaintenanceSchedules
            .AsNoTracking()
            .Where(m => m.AssetType == AssetType.EquipmentItem && m.AssetId == item.Id)
            .OrderByDescending(m => m.ScheduledDate)
            .ToListAsync(cancellationToken);

        var warnings = await _db.Warnings
            .AsNoTracking()
            .Where(w => w.RelatedEntityType == RelatedEntityType.EquipmentItem
                        && w.RelatedEntityId == item.Id
                        && w.Status == WarningStatus.Open)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        var detail = new EquipmentItemDetail
        {
            MaintenanceHistory = history.Select(m => ToSchedule(m, today)).ToList(),
            OpenWarnings = warnings.Select(ToWarning).ToList()
        };

        Fill(detail, item, names);
        return detail;
    }

    private static EquipmentItemSummary ToSummary(
        ItemEntity item, IReadOnlyDictionary<Guid, string> wardNames)
    {
        var summary = new EquipmentItemSummary();
        Fill(summary, item, wardNames);
        return summary;
    }

    private static void Fill(
        EquipmentItemSummary target, ItemEntity item, IReadOnlyDictionary<Guid, string> wardNames)
    {
        target.Id = item.Id;
        target.Name = item.Name;
        target.CategoryId = item.CategoryId;
        target.CategoryName = item.Category?.Name ?? string.Empty;
        target.Model = item.Model;
        target.Manufacturer = item.Manufacturer;
        target.AssetTag = item.AssetTag;
        target.WardId = item.WardId;
        target.WardName = item.WardId is { } ward && wardNames.TryGetValue(ward, out var name)
            ? name
            : null;
        target.Status = item.Status;
        target.NextMaintenanceDue = item.NextMaintenanceDue;

        if (target is EquipmentItem full)
        {
            full.PurchaseDate = item.PurchaseDate;
            full.SerialNumber = item.SerialNumber;
            full.AssignedToAdmissionId = item.AssignedToAdmissionId;
            full.AwaitingConfirmation = item.AwaitingConfirmation;
            full.ConfirmedByStaffId = item.ConfirmedByStaffId;
            full.ConfirmedAt = item.ConfirmedAt;
            full.CreatedAt = item.CreatedAt;
            full.UpdatedAt = item.UpdatedAt;
        }
    }

    internal static MaintenanceSchedule ToSchedule(
        Data.Entities.Equipment.MaintenanceSchedule schedule, DateOnly today)
        => new()
        {
            Id = schedule.Id,
            AssetType = schedule.AssetType,
            AssetId = schedule.AssetId,
            ScheduleType = schedule.ScheduleType,
            ScheduledDate = schedule.ScheduledDate,
            Status = schedule.Status == MaintenanceStatus.Scheduled && schedule.ScheduledDate < today
                ? MaintenanceStatus.Overdue
                : schedule.Status,
            PerformedByStaffId = schedule.PerformedByStaffId,
            CompletedAt = schedule.CompletedAt,
            Notes = schedule.Notes,
            CreatedBy = schedule.CreatedBy,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };

    internal static Warning ToWarning(WarningEntity warning)
        => new()
        {
            Id = warning.Id,
            Type = warning.Type,
            Severity = warning.Severity,
            RelatedEntityType = warning.RelatedEntityType,
            RelatedEntityId = warning.RelatedEntityId,
            WardId = warning.WardId,
            RecommendedAction = warning.RecommendedAction,
            Status = warning.Status,
            RaisedBy = warning.RaisedBy,
            WorkflowId = warning.WorkflowId,
            AcknowledgedByStaffId = warning.AcknowledgedByStaffId,
            AcknowledgedAt = warning.AcknowledgedAt,
            ResolvedAt = warning.ResolvedAt,
            CreatedAt = warning.CreatedAt,
            UpdatedAt = warning.UpdatedAt
        };

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
