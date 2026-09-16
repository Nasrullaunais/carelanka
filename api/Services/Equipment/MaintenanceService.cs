using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using ScheduleEntity = CareLanka.Api.Data.Entities.Equipment.MaintenanceSchedule;

namespace CareLanka.Api.Services.Equipment;

public sealed class MaintenanceService : IMaintenanceService
{
    private const string UnknownWardName = "Unknown ward";

    private static readonly IReadOnlyDictionary<MaintenanceType, int> IntervalMonths =
        new Dictionary<MaintenanceType, int>
        {
            [MaintenanceType.RoutineService] = 6,
            [MaintenanceType.Calibration] = 12
        };

    private readonly CareLankaDbContext _db;
    private readonly IBedOccupancyPort _occupancy;
    private readonly IWardDirectory _wards;
    private readonly ICurrentUser _currentUser;
    private readonly IEquipmentConfirmationCode _confirmationCode;

    public MaintenanceService(
        CareLankaDbContext db,
        IBedOccupancyPort occupancy,
        IWardDirectory wards,
        ICurrentUser currentUser,
        IEquipmentConfirmationCode confirmationCode)
    {
        _db = db;
        _occupancy = occupancy;
        _wards = wards;
        _currentUser = currentUser;
        _confirmationCode = confirmationCode;
    }

    public async Task<PagedResult<MaintenanceSchedule>> ListAsync(
        MaintenanceQuery query, CancellationToken cancellationToken = default)
    {
        var today = Today();
        var schedules = _db.MaintenanceSchedules.AsNoTracking().AsQueryable();

        if (query.AssetType is { } assetType)
        {
            schedules = schedules.Where(s => s.AssetType == assetType);
        }

        var wantsOverdue = query.Overdue == true || query.Status == MaintenanceStatus.Overdue;

        if (wantsOverdue)
        {
            schedules = schedules.Where(
                s => s.Status == MaintenanceStatus.Scheduled && s.ScheduledDate < today);
        }
        else
        {
            if (query.Overdue == false)
            {
                schedules = schedules.Where(
                    s => s.Status != MaintenanceStatus.Scheduled || s.ScheduledDate >= today);
            }

            if (query.Status is { } status)
            {
                schedules = schedules.Where(s => s.Status == status);
            }
        }

        var totalItems = await schedules.CountAsync(cancellationToken);

        var rows = await schedules
            .OrderBy(s => s.ScheduledDate)
            .ThenBy(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var labels = await LabelsAsync(rows, cancellationToken);

        return PagedResult<MaintenanceSchedule>.From(
            rows.Select(s => ToDto(s, labels, today)).ToList(),
            query.Page, query.PageSize, totalItems);
    }

    public async Task<MaintenanceSchedule> CreateAsync(
        CreateMaintenanceScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAssetExistsAsync(request.AssetType, request.AssetId, cancellationToken);

        if (request.AssetType == AssetType.Bed)
        {
            await EnsureBedMayBeServicedAsync(request.AssetId, cancellationToken);
        }

        var schedule = new ScheduleEntity
        {
            Id = Guid.NewGuid(),
            AssetType = request.AssetType,
            AssetId = request.AssetId,
            ScheduleType = request.ScheduleType,
            ScheduledDate = request.ScheduledDate,
            Status = MaintenanceStatus.Scheduled,
            Notes = Normalise(request.Notes),
            CreatedBy = RaisedBy.User
        };

        _db.MaintenanceSchedules.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(schedule, cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceSchedule>> ListOpenAsync(
        string? confirmationCode, CancellationToken cancellationToken = default)
    {
        _confirmationCode.Ensure(confirmationCode);

        var today = Today();

        var rows = await OpenSchedules()
            .AsNoTracking()
            .OrderBy(s => s.ScheduledDate)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var labels = await LabelsAsync(rows, cancellationToken);

        return rows.Select(s => ToDto(s, labels, today)).ToList();
    }

    public async Task<PendingEquipmentCount> CountOpenAsync(CancellationToken cancellationToken = default)
        => new() { Count = await OpenSchedules().CountAsync(cancellationToken) };

    public async Task<MaintenanceSchedule> ConfirmDoneAsync(
        Guid id, string? confirmationCode, CancellationToken cancellationToken = default)
    {
        _confirmationCode.Ensure(confirmationCode);

        var schedule = await GetByIdAsync(id, cancellationToken);

        if (schedule.Status is not (MaintenanceStatus.Scheduled or MaintenanceStatus.InProgress))
        {
            throw new ConflictException(
                MessageCode.MaintenanceNotCompletable, EnumWire.ToWire(schedule.Status));
        }

        var now = DateTimeOffset.UtcNow;

        schedule.Status = MaintenanceStatus.Completed;
        schedule.CompletedAt = now;
        schedule.PerformedByStaffId = _currentUser.Id;

        await PutBackInServiceAsync(schedule, cancellationToken);
        await CloseWarningsAsync(schedule, now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(schedule, cancellationToken);
    }

    private IQueryable<ScheduleEntity> OpenSchedules()
        => _db.MaintenanceSchedules.Where(
            s => s.Status == MaintenanceStatus.Scheduled || s.Status == MaintenanceStatus.InProgress);

    public Task<ScheduleEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<ScheduleEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Maintenance schedule", id);

    private async Task PutBackInServiceAsync(
        ScheduleEntity schedule, CancellationToken cancellationToken)
    {
        if (schedule.AssetType == AssetType.EquipmentItem)
        {
            var item = await _db.EquipmentItems
                .FirstOrDefaultAsync(i => i.Id == schedule.AssetId, cancellationToken);

            if (item is null)
            {
                return;
            }

            if (item.Status == EquipmentStatus.Maintenance)
            {
                item.Status = EquipmentStatus.Available;
            }

            if (IntervalMonths.TryGetValue(schedule.ScheduleType, out var months))
            {
                item.NextMaintenanceDue = DateOnly.FromDateTime(
                    DateTimeOffset.UtcNow.UtcDateTime).AddMonths(months);
            }

            return;
        }

        var bed = await _db.Beds.FirstOrDefaultAsync(b => b.Id == schedule.AssetId, cancellationToken);

        if (bed is { Condition: BedCondition.OutOfService })
        {
            bed.Condition = BedCondition.Usable;
        }
    }

    private async Task CloseWarningsAsync(
        ScheduleEntity schedule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var related = schedule.AssetType == AssetType.Bed
            ? RelatedEntityType.Bed
            : RelatedEntityType.EquipmentItem;

        var open = await _db.Warnings
            .Where(w => w.RelatedEntityType == related
                        && w.RelatedEntityId == schedule.AssetId
                        && w.Status == WarningStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var warning in open)
        {
            warning.Status = WarningStatus.ActionTaken;
            warning.ResolvedAt = now;
        }
    }

    private async Task EnsureAssetExistsAsync(
        AssetType assetType, Guid assetId, CancellationToken cancellationToken)
    {
        if (assetType == AssetType.Bed)
        {
            if (!await _db.Beds.AnyAsync(b => b.Id == assetId, cancellationToken))
            {
                throw new NotFoundException("Bed", assetId);
            }

            return;
        }

        var item = await _db.EquipmentItems.AsNoTracking()
            .Where(i => i.Id == assetId)
            .Select(i => new { i.Name, i.AwaitingConfirmation })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Equipment item", assetId);

        if (item.AwaitingConfirmation)
        {
            throw new ConflictException(MessageCode.EquipmentAwaitingConfirmation, item.Name);
        }
    }

    private async Task EnsureBedMayBeServicedAsync(Guid bedId, CancellationToken cancellationToken)
    {
        var bed = await _db.Beds.FirstAsync(b => b.Id == bedId, cancellationToken);
        var occupancy = await _occupancy.GetOccupancyAsync(bedId, cancellationToken);

        if (!occupancy.MayTakeOutOfService)
        {
            throw new ConflictException(MessageCode.BedOccupied, bed.BedNumber);
        }
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    private async Task<MaintenanceSchedule> ToDtoAsync(
        ScheduleEntity schedule, CancellationToken cancellationToken)
    {
        var labels = await LabelsAsync(new[] { schedule }, cancellationToken);

        return ToDto(schedule, labels, Today());
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LabelsAsync(
        IReadOnlyCollection<ScheduleEntity> schedules, CancellationToken cancellationToken)
    {
        var labels = new Dictionary<Guid, string>();

        var itemIds = schedules.Where(s => s.AssetType == AssetType.EquipmentItem)
            .Select(s => s.AssetId).Distinct().ToList();

        var bedIds = schedules.Where(s => s.AssetType == AssetType.Bed)
            .Select(s => s.AssetId).Distinct().ToList();

        if (itemIds.Count > 0)
        {
            var items = await _db.EquipmentItems.AsNoTracking()
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Name, i.AssetTag })
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                labels[item.Id] = $"{item.Name} (asset tag {item.AssetTag})";
            }
        }

        if (bedIds.Count > 0)
        {
            var beds = await _db.Beds.AsNoTracking()
                .Where(b => bedIds.Contains(b.Id))
                .Select(b => new { b.Id, b.WardId, b.BedNumber })
                .ToListAsync(cancellationToken);

            var wardNames = beds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _wards.GetWardNamesAsync(
                    beds.Select(b => b.WardId).Distinct().ToList(), cancellationToken);

            foreach (var bed in beds)
            {
                var ward = wardNames.TryGetValue(bed.WardId, out var name) ? name : UnknownWardName;
                labels[bed.Id] = $"{ward}, Bed {bed.BedNumber}";
            }
        }

        return labels;
    }

    private static MaintenanceSchedule ToDto(
        ScheduleEntity schedule, IReadOnlyDictionary<Guid, string> labels, DateOnly today)
        => new()
        {
            Id = schedule.Id,
            AssetType = schedule.AssetType,
            AssetId = schedule.AssetId,
            AssetLabel = labels.TryGetValue(schedule.AssetId, out var label)
                ? label
                : "Unknown asset",
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

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
