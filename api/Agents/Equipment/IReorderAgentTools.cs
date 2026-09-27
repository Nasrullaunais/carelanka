namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// The allow-list as a type, same shape as <c>ICareAgentTools</c>. One read tool and nothing
/// else - there is no write tool here at all, because applying a suggested threshold is a
/// separate, deliberate action a human takes through the plain item-edit endpoint, never
/// something this agent does itself.
/// </summary>
public interface IReorderAgentTools
{
    Task<ReorderItemFacts?> GetItemAsync(Guid pharmacyItemId, CancellationToken ct = default);

    Task<ReorderDispensingHistoryFacts> GetDispensingHistoryAsync(
        Guid pharmacyItemId, int days, CancellationToken ct = default);
}

public sealed record ReorderItemFacts(
    string Name, string Unit, int CurrentThreshold, int CurrentQuantityOnHand);

public sealed record ReorderDailyDispensed(DateOnly Date, int Quantity);

public sealed record ReorderDispensingHistoryFacts(
    int WindowDays, IReadOnlyList<ReorderDailyDispensed> DailyDispensed);
