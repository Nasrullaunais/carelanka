using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// The one seam a language model plugs into for this agent. Given the item and its dispensing
/// history, produce a suggested threshold and a one-sentence reason - or nothing, if no model is
/// configured or the call fails. Every caller has <see cref="DeterministicReorderAdvisor"/> to
/// fall back to, so nothing about the answer depends on a model being reachable.
/// </summary>
public interface IReorderAdvisor
{
    Task<ReorderDraftCandidate> SuggestAsync(ReorderAdviceContext context, CancellationToken ct = default);
}

public sealed record ReorderAdviceContext(
    ReorderItemFacts Item, ReorderDispensingHistoryFacts History);

/// <summary>
/// <paramref name="Source"/> is what tells a reviewer whether they are reading the model's own
/// reasoning about this medicine or the fixed formula - the two numbers can look equally
/// plausible by eye.
/// </summary>
public sealed record ReorderDraftCandidate(
    int SuggestedThreshold,
    string Reasoning,
    ReorderSuggestionSource Source = ReorderSuggestionSource.Model);
