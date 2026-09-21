namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Where the draft on a care recommendation actually came from. Only <see cref="Model"/> is a note
/// written about this patient; the other two are the same fixed backup sentence, and the reviewer
/// needs to be told which one they are looking at.
/// </summary>
public enum CareDraftSource
{
    /// <summary>The model answered and its draft passed CR1-CR5.</summary>
    Model,

    /// <summary>No model answered - no key, a spent allowance, a busy provider or a timeout.</summary>
    ModelUnavailable,

    /// <summary>The model answered but its draft broke a safety rule, so it was thrown away.</summary>
    ModelRejected
}
