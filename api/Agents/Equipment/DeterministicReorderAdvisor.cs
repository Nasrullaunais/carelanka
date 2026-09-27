using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// The fallback drafter, used with no model configured, a dead key, an exhausted quota, a
/// timeout, or a model answer the sanity check rejected. Average daily dispensed over the window
/// times a lead-time buffer, rounded up - the same number a pharmacist would reach for with a
/// calculator. No dispensing at all in the window means there is nothing to reason from, so the
/// current threshold is kept, with a note saying why.
/// </summary>
public sealed class DeterministicReorderAdvisor : IReorderAdvisor
{
    private readonly int _leadTimeDays;

    public DeterministicReorderAdvisor(int leadTimeDays = 7)
    {
        _leadTimeDays = leadTimeDays;
    }

    public Task<ReorderDraftCandidate> SuggestAsync(
        ReorderAdviceContext context, CancellationToken ct = default)
    {
        var totalDispensed = context.History.DailyDispensed.Sum(day => day.Quantity);

        if (totalDispensed == 0 || context.History.WindowDays <= 0)
        {
            return Task.FromResult(new ReorderDraftCandidate(
                context.Item.CurrentThreshold,
                $"No {context.Item.Name} has been dispensed in the last {context.History.WindowDays} " +
                "days, so the threshold is left unchanged.",
                ReorderSuggestionSource.ModelUnavailable));
        }

        var averageDaily = (double)totalDispensed / context.History.WindowDays;
        var suggested = (int)Math.Ceiling(averageDaily * _leadTimeDays);

        var reasoning =
            $"Based on an average of {averageDaily:0.#} {context.Item.Unit} dispensed per day over " +
            $"the last {context.History.WindowDays} days, {_leadTimeDays} days' worth is " +
            $"{suggested} {context.Item.Unit}.";

        return Task.FromResult(
            new ReorderDraftCandidate(suggested, reasoning, ReorderSuggestionSource.ModelUnavailable));
    }
}
