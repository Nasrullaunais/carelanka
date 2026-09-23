using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// The one read tool, a plain query against tables this component already owns - same shape as
/// <c>CareAgentTools</c>. Grouped in memory rather than in SQL: the window is capped at a few
/// months of one medicine's movements, and a plain query is easier to trust than a translated
/// date-truncation.
/// </summary>
public sealed class ReorderAgentTools : IReorderAgentTools
{
    private readonly CareLankaDbContext _db;

    public ReorderAgentTools(CareLankaDbContext db) => _db = db;

    public async Task<ReorderItemFacts?> GetItemAsync(Guid pharmacyItemId, CancellationToken ct = default)
    {
        var item = await _db.PharmacyItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == pharmacyItemId, ct);

        return item is null
            ? null
            : new ReorderItemFacts(item.Name, item.Unit, item.ReorderThreshold, item.QuantityOnHand);
    }

    public async Task<ReorderDispensingHistoryFacts> GetDispensingHistoryAsync(
        Guid pharmacyItemId, int days, CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var rows = await _db.PharmacyTransactions.AsNoTracking()
            .Where(t => t.PharmacyItemId == pharmacyItemId
                && t.Type == PharmacyTransactionType.Dispensed
                && t.CreatedAt >= since)
            .Select(t => new { t.CreatedAt, t.Quantity })
            .ToListAsync(ct);

        var daily = rows
            .GroupBy(row => DateOnly.FromDateTime(row.CreatedAt.UtcDateTime))
            .Select(group => new ReorderDailyDispensed(group.Key, group.Sum(row => row.Quantity)))
            .OrderBy(day => day.Date)
            .ToList();

        return new ReorderDispensingHistoryFacts(days, daily);
    }
}
