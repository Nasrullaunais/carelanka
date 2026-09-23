namespace CareLanka.Api.Data.Enums;

/// <summary>
/// Where a reorder suggestion's numbers actually came from. Only <see cref="Model"/> is the
/// language model reasoning about this medicine's own history; the other two are the fixed
/// formula, and the reviewer needs to be told which one they are looking at - same reasoning as
/// <c>CareDraftSource</c>.
/// </summary>
public enum ReorderSuggestionSource
{
    /// <summary>The model answered and its draft passed the deterministic sanity check.</summary>
    Model,

    /// <summary>No model answered - no key, a spent allowance, a busy provider or a timeout.</summary>
    ModelUnavailable,

    /// <summary>The model answered but its number failed the sanity check, so it was thrown away.</summary>
    ModelRejected
}
