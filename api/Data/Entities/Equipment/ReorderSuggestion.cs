using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

/// <summary>
/// One run of the reorder-threshold advisor for one medicine. Created before the agent runs -
/// the workflow row points at this one's id - and filled in once the run completes. Applying the
/// suggested number is a separate, deliberate write on <see cref="PharmacyItem.ReorderThreshold"/>
/// a human makes afterwards; nothing here ever changes the threshold itself.
/// </summary>
public class ReorderSuggestion : AuditedEntity
{
    public Guid PharmacyItemId { get; set; }

    /// <summary>Snapshots at submit time, so the workflow read shows what the suggestion was
    /// reasoned against even if someone edits the item while the run is in flight.</summary>
    public int CurrentThreshold { get; set; }

    public int CurrentQuantityOnHand { get; set; }

    public int? SuggestedThreshold { get; set; }

    public string? Reasoning { get; set; }

    public ReorderSuggestionSource? Source { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
