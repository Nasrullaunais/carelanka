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

    // How far ahead a completed service pushes the next one. Placeholder numbers, not a
    // clinical decision: nothing in the plan or the spec fixes an interval, and the group
    // has to agree one the same way it has to agree COST_THRESHOLD (plan section 16, open
    // question 3). Changing them is this table and nothing else.
    //
    // A repair is deliberately absent. It is unplanned work, so finishing one does not mean
    // the routine service clock restarts - an item repaired in March is still due its annual
    // service in June.
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

    public MaintenanceService(
        CareLankaDbContext db,
        IBedOccupancyPort occupancy,
        IWardDirectory wards,
        ICurrentUser currentUser)
    {
        _db = db;
        _occupancy = occupancy;
        _wards = wards;
        _currentUser = currentUser;
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

        // Overdue is derived, so it can never be matched by equality on the column. Both the
        // flag and Status=overdue resolve to the same predicate, because they are the same
        // question asked two ways.
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

        // Soonest first: a task list is read to answer "what is next", not "what is newest".
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
        // Throws 404 rather than leaving a row pointing at nothing. The reference is
        // polymorphic, so there is no foreign key to catch a bad id for us.
        await EnsureAssetExistsAsync(request.AssetType, request.AssetId, cancellationToken);

        // The safety rule of this component. Asked before anything is written, in this
        // request, because only Patient Management knows whether someone is in the bed.
        // Maintenance never evicts a patient.
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
            // A person booked this, not the sweep. The agent-performance report is exactly
            // the question of which, so it is recorded rather than assumed.
            CreatedBy = RaisedBy.User
        };

        _db.MaintenanceSchedules.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(schedule, cancellationToken);
    }

    public async Task<MaintenanceSchedule> CompleteAsync(
        Guid id, string? notes, CancellationToken cancellationToken = default)
    {
        var schedule = await GetByIdAsync(id, cancellationToken);

        // Only work that is still outstanding can be finished. Overdue counts: it is stored
        // as scheduled and only reads as overdue, so the date passing does not make a task
        // impossible to close - which would be the worst possible reading of "overdue".
        if (schedule.Status != MaintenanceStatus.Scheduled)
        {
            throw new ConflictException(
                MessageCode.MaintenanceNotCompletable, EnumWire.ToWire(schedule.Status));
        }

        var now = DateTimeOffset.UtcNow;

        schedule.Status = MaintenanceStatus.Completed;
        schedule.CompletedAt = now;
        // From the token, never the body: a caller cannot record work against someone else.
        schedule.PerformedByStaffId = _currentUser.Id;

        if (Normalise(notes) is { } written)
        {
            schedule.Notes = written;
        }

        await PutBackInServiceAsync(schedule, cancellationToken);
        await CloseWarningsAsync(schedule, now, cancellationToken);

        // One SaveChanges for the whole thing. A failure part-way through must not leave a
        // task marked done against an item still sitting in maintenance.
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(schedule, cancellationToken);
    }

    public Task<ScheduleEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<ScheduleEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await FindByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Maintenance schedule", id);

    /// <summary>
    /// The asset goes back to work, and its next service is booked forward. Both belong to
    /// completing the task rather than to a separate call, because an item left in
    /// maintenance after its service is finished is invisible stock.
    /// </summary>
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

            // Retired is terminal, so servicing does not revive it. Anything else that was
            // out for this work comes back available.
            if (item.Status == EquipmentStatus.Maintenance)
            {
                item.Status = EquipmentStatus.Available;
            }

            if (IntervalMonths.TryGetValue(schedule.ScheduleType, out var months))
            {
                // Measured from the work being done, not from the date it was booked for:
                // a service done three weeks late still buys a full interval.
                item.NextMaintenanceDue = DateOnly.FromDateTime(
                    DateTimeOffset.UtcNow.UtcDateTime).AddMonths(months);
            }

            return;
        }

        var bed = await _db.Beds.FirstOrDefaultAsync(b => b.Id == schedule.AssetId, cancellationToken);

        // Beds are not in the spec's wording for this endpoint, which names equipment only.
        // Left out, a serviced bed stays out of service forever, so it is restored on the
        // same reasoning. Only a bed actually withdrawn for the work is touched.
        if (bed is { Condition: BedCondition.OutOfService })
        {
            bed.Condition = BedCondition.Usable;
        }
    }

    /// <summary>
    /// A warning that led to this work is answered by the work, not by someone remembering
    /// to tick it off. Anything still open against this asset moves to action_taken.
    /// </summary>
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
        var exists = assetType == AssetType.Bed
            ? await _db.Beds.AnyAsync(b => b.Id == assetId, cancellationToken)
            : await _db.EquipmentItems.AnyAsync(i => i.Id == assetId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException(
                assetType == AssetType.Bed ? "Bed" : "Equipment item", assetId);
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

    /// <summary>
    /// One pass for the whole page rather than a lookup per row, and one ward call rather
    /// than one per bed. A task list of GUIDs is unusable on a phone.
    /// </summary>
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
            // An asset deleted out from under a schedule still has to render as something.
            AssetLabel = labels.TryGetValue(schedule.AssetId, out var label)
                ? label
                : "Unknown asset",
            ScheduleType = schedule.ScheduleType,
            ScheduledDate = schedule.ScheduledDate,
            // Derived here rather than stored, so nothing has to sweep the table at midnight
            // to keep it honest.
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
