using System.Globalization;
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
using WarningEntity = CareLanka.Api.Data.Entities.Equipment.Warning;

namespace CareLanka.Api.Services.Equipment;

// The deterministic warning sweep (build step 7). Every warning it raises comes from a fixed rule
// written here - no model is involved - so "stock at or below the reorder level" fires every time,
// not whenever something happens to notice. The monitoring agent reads these warnings later; it
// never decides whether one exists.
public sealed class WarningService : IWarningService
{
    // Days of dispensing the low-stock rule looks back over to estimate how long stock will last.
    private const int UsageWindowDays = 14;

    // Stock that will run out sooner than this at the recent rate is low, whatever the threshold says.
    private const double MinimumDaysOfSupply = 3;

    private const int RecommendedActionMaxLength = 500;

    // The timer and the Run check button can land together; one sweep at a time keeps them from
    // both raising the same warning. The unique index on live sweep warnings backs this up.
    private static readonly SemaphoreSlim SweepLock = new(1, 1);

    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;
    private readonly EquipmentOptions _options;
    private readonly IEquipmentConfirmationCode _confirmationCode;

    public WarningService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        TimeProvider clock,
        IOptions<EquipmentOptions> options,
        IEquipmentConfirmationCode confirmationCode)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _options = options.Value;
        _confirmationCode = confirmationCode;
    }

    public async Task<PagedResult<Warning>> ListAsync(
        WarningQuery query, CancellationToken cancellationToken = default)
    {
        // A warning marked done has left the list; it is kept only as a record.
        var warnings = _db.Warnings.AsNoTracking().Where(w => w.ClearedAt == null);

        if (query.Status is { } status)
        {
            warnings = warnings.Where(w => w.Status == status);
        }

        if (query.Severity is { } severity)
        {
            warnings = warnings.Where(w => w.Severity == severity);
        }

        if (query.Type is { } type)
        {
            warnings = warnings.Where(w => w.Type == type);
        }

        var totalItems = await warnings.CountAsync(cancellationToken);

        // Severity is stored as text, so sorting on the column would put "critical" before "high"
        // only by luck of the alphabet. Rank it explicitly: worst first, then newest.
        var rows = await warnings
            .OrderByDescending(w => w.Severity == WarningSeverity.Critical ? 3
                : w.Severity == WarningSeverity.High ? 2
                : w.Severity == WarningSeverity.Medium ? 1
                : 0)
            .ThenByDescending(w => w.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var labels = await LabelsAsync(rows, cancellationToken);

        return PagedResult<Warning>.From(
            rows.Select(w => ToDto(w, labels)).ToList(), query.Page, query.PageSize, totalItems);
    }

    public async Task<Warning> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warning = await _db.Warnings.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Warning", id);

        if (warning.Status is WarningStatus.ActionTaken or WarningStatus.Dismissed)
        {
            throw new ConflictException(MessageCode.WarningClosed, EnumWire.ToWire(warning.Status));
        }

        // Acknowledging twice is harmless: the first person to see it stays on record.
        if (warning.Status == WarningStatus.Open)
        {
            warning.Status = WarningStatus.Acknowledged;
            warning.AcknowledgedByStaffId = _currentUser.Id;
            warning.AcknowledgedAt = _clock.GetUtcNow();

            await _db.SaveChangesAsync(cancellationToken);
        }

        var labels = await LabelsAsync(new[] { warning }, cancellationToken);

        return ToDto(warning, labels);
    }

    public async Task ClearAsync(
        Guid id, string? confirmationCode, CancellationToken cancellationToken = default)
    {
        _confirmationCode.Ensure(confirmationCode);

        var warning = await _db.Warnings
            .FirstOrDefaultAsync(w => w.Id == id && w.ClearedAt == null, cancellationToken)
            ?? throw new NotFoundException("Warning", id);

        if (warning.Status != WarningStatus.ActionTaken)
        {
            throw new ConflictException(MessageCode.WarningNotResolved, EnumWire.ToWire(warning.Status));
        }

        warning.ClearedAt = _clock.GetUtcNow();
        warning.ClearedByStaffId = _currentUser.Id;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WarningSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        await SweepLock.WaitAsync(cancellationToken);

        try
        {
            return await RunSweepAsync(cancellationToken);
        }
        finally
        {
            SweepLock.Release();
        }
    }

    private async Task<WarningSweepResult> RunSweepAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var today = HospitalTime.Today(now);

        var findings = new Dictionary<FindingKey, Finding>();

        foreach (var finding in await LowStockAsync(now, cancellationToken))
        {
            findings[finding.Key] = finding;
        }

        foreach (var finding in await ExpiringAsync(today, cancellationToken))
        {
            findings[finding.Key] = finding;
        }

        foreach (var finding in await MaintenanceOverdueAsync(today, cancellationToken))
        {
            findings[finding.Key] = finding;
        }

        // Only the sweep's own warnings are reconciled. A fault somebody reported stays with the
        // maintenance unit, and closes when they confirm the repair.
        var live = await _db.Warnings
            .Where(w => w.RaisedBy == RaisedBy.System
                        && (w.Status == WarningStatus.Open || w.Status == WarningStatus.Acknowledged))
            .ToListAsync(cancellationToken);

        int raised = 0, updated = 0, resolved = 0;

        foreach (var warning in live)
        {
            var key = new FindingKey(warning.Type, warning.RelatedEntityType, warning.RelatedEntityId);

            if (!findings.Remove(key, out var finding))
            {
                warning.Status = WarningStatus.ActionTaken;
                warning.ResolvedAt = now;
                resolved++;
                continue;
            }

            if (warning.Severity == finding.Severity
                && warning.RecommendedAction == finding.RecommendedAction
                && warning.WardId == finding.WardId)
            {
                continue;
            }

            // Somebody acknowledged it at "medium"; that does not cover it becoming critical.
            if (Rank(finding.Severity) > Rank(warning.Severity)
                && warning.Status == WarningStatus.Acknowledged)
            {
                warning.Status = WarningStatus.Open;
                warning.AcknowledgedByStaffId = null;
                warning.AcknowledgedAt = null;
            }

            warning.Severity = finding.Severity;
            warning.RecommendedAction = finding.RecommendedAction;
            warning.WardId = finding.WardId;
            updated++;
        }

        foreach (var finding in findings.Values)
        {
            _db.Warnings.Add(new WarningEntity
            {
                Id = Guid.NewGuid(),
                Type = finding.Key.Type,
                Severity = finding.Severity,
                RelatedEntityType = finding.Key.RelatedEntityType,
                RelatedEntityId = finding.Key.RelatedEntityId,
                WardId = finding.WardId,
                RecommendedAction = finding.RecommendedAction,
                Status = WarningStatus.Open,
                RaisedBy = RaisedBy.System
            });
            raised++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new WarningSweepResult
        {
            Raised = raised,
            Updated = updated,
            Resolved = resolved,
            StillOpen = live.Count - resolved + raised,
            RanAt = now
        };
    }

    // Rule 1 - low stock: on hand at or below the reorder level (the same test the pharmacy page
    // highlights), or fewer than three days left at the last fortnight's dispensing rate.
    private async Task<IReadOnlyList<Finding>> LowStockAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now.AddDays(-UsageWindowDays);

        var dispensed = await _db.PharmacyTransactions.AsNoTracking()
            .Where(t => t.Type == PharmacyTransactionType.Dispensed && t.CreatedAt >= since)
            .GroupBy(t => t.PharmacyItemId)
            .Select(g => new { ItemId = g.Key, Total = g.Sum(t => t.Quantity) })
            .ToDictionaryAsync(g => g.ItemId, g => g.Total, cancellationToken);

        var items = await _db.PharmacyItems.AsNoTracking()
            .Select(i => new { i.Id, i.Name, i.Unit, i.QuantityOnHand, i.ReorderThreshold })
            .ToListAsync(cancellationToken);

        var findings = new List<Finding>();

        foreach (var item in items)
        {
            var perDay = dispensed.TryGetValue(item.Id, out var used) ? used / (double)UsageWindowDays : 0;
            double? daysLeft = perDay > 0 ? item.QuantityOnHand / perDay : null;

            var belowThreshold = item.QuantityOnHand <= item.ReorderThreshold;
            var runningOut = daysLeft < MinimumDaysOfSupply;

            if (!belowThreshold && !runningOut)
            {
                continue;
            }

            WarningSeverity severity;
            string action;

            if (item.QuantityOnHand == 0)
            {
                severity = WarningSeverity.Critical;
                action = $"{item.Name} is out of stock (reorder level {item.ReorderThreshold} {item.Unit}). Reorder now.";
            }
            else
            {
                severity = item.QuantityOnHand * 2 <= item.ReorderThreshold || runningOut
                    ? WarningSeverity.High
                    : WarningSeverity.Medium;

                action = belowThreshold
                    ? $"{item.Name} is down to {item.QuantityOnHand} {item.Unit}, at or below the reorder level of {item.ReorderThreshold}. Reorder."
                    : $"{item.Name} has {item.QuantityOnHand} {item.Unit} left. Reorder.";
            }

            if (runningOut && item.QuantityOnHand > 0)
            {
                action += string.Format(
                    CultureInfo.InvariantCulture,
                    " At the last {0} days' dispensing rate it lasts about {1:0.#} more days.",
                    UsageWindowDays, daysLeft);
            }

            findings.Add(new Finding(
                new FindingKey(WarningType.LowStock, RelatedEntityType.PharmacyItem, item.Id),
                null, severity, action));
        }

        return findings;
    }

    // Rule 2 - expiring medicine: any batch with boxes left that expires within the window, or has
    // already expired. One warning per medicine, naming each batch, worst batch sets the severity.
    private async Task<IReadOnlyList<Finding>> ExpiringAsync(
        DateOnly today, CancellationToken cancellationToken)
    {
        var limit = today.AddDays(_options.ExpiryWarningDays);

        // Reading the medicine through the batch applies its soft-delete filter, so a removed
        // medicine's batches are left out.
        var batches = await _db.PharmacyBatches.AsNoTracking()
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate != null && b.ExpiryDate <= limit)
            .Select(b => new
            {
                b.PharmacyItemId,
                ItemName = b.Item.Name,
                b.Item.Unit,
                b.BatchNumber,
                b.QuantityOnHand,
                ExpiryDate = b.ExpiryDate!.Value
            })
            .ToListAsync(cancellationToken);

        return batches
            .GroupBy(b => b.PharmacyItemId)
            .Select(group =>
            {
                var ordered = group.OrderBy(b => b.ExpiryDate).ThenBy(b => b.BatchNumber).ToList();
                var first = ordered[0];
                var soonest = first.ExpiryDate.DayNumber - today.DayNumber;

                var severity = soonest switch
                {
                    < 0 => WarningSeverity.Critical,
                    <= 7 => WarningSeverity.High,
                    <= 14 => WarningSeverity.Medium,
                    _ => WarningSeverity.Low
                };

                var parts = ordered.Select(b =>
                {
                    var days = b.ExpiryDate.DayNumber - today.DayNumber;
                    var when = days switch
                    {
                        < 0 => $"expired {-days} day{Plural(-days)} ago",
                        0 => "expires today",
                        _ => $"expires in {days} day{Plural(days)}"
                    };

                    return $"batch {b.BatchNumber}, {b.QuantityOnHand} {first.Unit}, {when} "
                           + $"({b.ExpiryDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)})";
                });

                var advice = soonest < 0
                    ? "Take the expired boxes off the shelf and record them as expired-removed."
                    : "Dispense these first, or plan to write them off.";

                return new Finding(
                    new FindingKey(WarningType.MedicineExpiring, RelatedEntityType.PharmacyItem, group.Key),
                    null, severity, $"{first.ItemName}: {string.Join("; ", parts)}. {advice}");
            })
            .ToList();
    }

    // Rule 3 - overdue maintenance: a machine past its next service date with nothing booked, or a
    // service or calibration booked for a day that has gone by. Repairs are left out on purpose:
    // an open repair already has its fault warning, and one problem should be one warning.
    private async Task<IReadOnlyList<Finding>> MaintenanceOverdueAsync(
        DateOnly today, CancellationToken cancellationToken)
    {
        var lateJobs = await _db.MaintenanceSchedules.AsNoTracking()
            .Where(s => s.Status == MaintenanceStatus.Scheduled
                        && s.ScheduleType != MaintenanceType.Repair
                        && s.ScheduledDate < today)
            .Select(s => new { s.AssetType, s.AssetId, s.ScheduleType, s.ScheduledDate })
            .ToListAsync(cancellationToken);

        // Booked for today or later means somebody is already on it.
        var booked = await _db.MaintenanceSchedules.AsNoTracking()
            .Where(s => s.AssetType == AssetType.EquipmentItem
                        && (s.Status == MaintenanceStatus.InProgress
                            || (s.Status == MaintenanceStatus.Scheduled && s.ScheduledDate >= today)))
            .Select(s => s.AssetId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var dueItems = await _db.EquipmentItems.AsNoTracking()
            .Where(i => i.NextMaintenanceDue != null
                        && i.NextMaintenanceDue < today
                        && !i.AwaitingConfirmation
                        && i.Status != EquipmentStatus.Retired
                        && i.Status != EquipmentStatus.Maintenance
                        && !booked.Contains(i.Id))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        var itemIds = lateJobs.Where(j => j.AssetType == AssetType.EquipmentItem)
            .Select(j => j.AssetId).Concat(dueItems).Distinct().ToList();

        var items = await _db.EquipmentItems.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id)
                        && !i.AwaitingConfirmation
                        && i.Status != EquipmentStatus.Retired)
            .Select(i => new { i.Id, i.Name, i.AssetTag, i.WardId, i.NextMaintenanceDue })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var bedIds = lateJobs.Where(j => j.AssetType == AssetType.Bed).Select(j => j.AssetId).Distinct().ToList();

        var beds = await _db.Beds.AsNoTracking()
            .Where(b => bedIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BedNumber, b.WardId })
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        var findings = new List<Finding>();

        foreach (var itemId in itemIds)
        {
            if (!items.TryGetValue(itemId, out var item))
            {
                continue;
            }

            var late = lateJobs.Where(j => j.AssetType == AssetType.EquipmentItem && j.AssetId == itemId)
                .OrderBy(j => j.ScheduledDate).FirstOrDefault();

            var name = $"{item.Name} (asset tag {item.AssetTag})";
            int daysLate;
            string action;

            if (late is not null)
            {
                daysLate = today.DayNumber - late.ScheduledDate.DayNumber;
                action = $"{name}: the {Describe(late.ScheduleType)} booked for {Format(late.ScheduledDate)} "
                         + $"is {daysLate} day{Plural(daysLate)} overdue. Do it, or rebook it.";
            }
            else
            {
                var due = item.NextMaintenanceDue!.Value;
                daysLate = today.DayNumber - due.DayNumber;
                action = $"{name} was due for service on {Format(due)}, {daysLate} day{Plural(daysLate)} ago, "
                         + "and nothing is booked. Book it with the maintenance unit.";
            }

            findings.Add(new Finding(
                new FindingKey(WarningType.MaintenanceOverdue, RelatedEntityType.EquipmentItem, itemId),
                item.WardId, OverdueSeverity(daysLate), action));
        }

        foreach (var bedId in bedIds)
        {
            if (!beds.TryGetValue(bedId, out var bed))
            {
                continue;
            }

            var late = lateJobs.Where(j => j.AssetType == AssetType.Bed && j.AssetId == bedId)
                .OrderBy(j => j.ScheduledDate).First();
            var daysLate = today.DayNumber - late.ScheduledDate.DayNumber;

            findings.Add(new Finding(
                new FindingKey(WarningType.MaintenanceOverdue, RelatedEntityType.Bed, bedId),
                bed.WardId,
                OverdueSeverity(daysLate),
                $"Bed {bed.BedNumber}: the {Describe(late.ScheduleType)} booked for {Format(late.ScheduledDate)} "
                + $"is {daysLate} day{Plural(daysLate)} overdue. Do it, or rebook it."));
        }

        return findings;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LabelsAsync(
        IReadOnlyCollection<WarningEntity> warnings, CancellationToken cancellationToken)
    {
        var labels = new Dictionary<Guid, string>();

        List<Guid> Ids(RelatedEntityType type)
            => warnings.Where(w => w.RelatedEntityType == type).Select(w => w.RelatedEntityId).Distinct().ToList();

        // Closed warnings can point at something since removed; the name is still worth showing.
        var medicineIds = Ids(RelatedEntityType.PharmacyItem);
        if (medicineIds.Count > 0)
        {
            foreach (var m in await _db.PharmacyItems.IgnoreQueryFilters().AsNoTracking()
                         .Where(i => medicineIds.Contains(i.Id))
                         .Select(i => new { i.Id, i.Name })
                         .ToListAsync(cancellationToken))
            {
                labels[m.Id] = m.Name;
            }
        }

        var itemIds = Ids(RelatedEntityType.EquipmentItem);
        if (itemIds.Count > 0)
        {
            foreach (var i in await _db.EquipmentItems.IgnoreQueryFilters().AsNoTracking()
                         .Where(i => itemIds.Contains(i.Id))
                         .Select(i => new { i.Id, i.Name, i.AssetTag })
                         .ToListAsync(cancellationToken))
            {
                labels[i.Id] = $"{i.Name} ({i.AssetTag})";
            }
        }

        var bedIds = Ids(RelatedEntityType.Bed);
        if (bedIds.Count > 0)
        {
            foreach (var b in await _db.Beds.IgnoreQueryFilters().AsNoTracking()
                         .Where(b => bedIds.Contains(b.Id))
                         .Select(b => new { b.Id, b.BedNumber })
                         .ToListAsync(cancellationToken))
            {
                labels[b.Id] = $"Bed {b.BedNumber}";
            }
        }

        return labels;
    }

    private static Warning ToDto(WarningEntity warning, IReadOnlyDictionary<Guid, string> labels)
    {
        var dto = EquipmentItemService.ToWarning(warning);
        dto.RelatedEntityLabel = labels.GetValueOrDefault(warning.RelatedEntityId);
        return dto;
    }

    private static WarningSeverity OverdueSeverity(int daysLate)
        => daysLate > 30 ? WarningSeverity.High : WarningSeverity.Medium;

    private static int Rank(WarningSeverity severity) => severity switch
    {
        WarningSeverity.Critical => 3,
        WarningSeverity.High => 2,
        WarningSeverity.Medium => 1,
        _ => 0
    };

    private static string Describe(MaintenanceType type) => type switch
    {
        MaintenanceType.Calibration => "calibration",
        MaintenanceType.Repair => "repair",
        _ => "routine service"
    };

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private readonly record struct FindingKey(WarningType Type, RelatedEntityType RelatedEntityType, Guid RelatedEntityId);

    private sealed record Finding(FindingKey Key, Guid? WardId, WarningSeverity Severity, string Text)
    {
        public string RecommendedAction { get; } =
            Text.Length <= RecommendedActionMaxLength ? Text : Text[..(RecommendedActionMaxLength - 1)] + "…";
    }
}
