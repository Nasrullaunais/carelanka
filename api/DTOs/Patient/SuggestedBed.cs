using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One selectable bed. The best pick and every alternative use this same shape so that every
/// entry is committed the same way, with the same one button.
/// </summary>
public sealed class SuggestedBed
{
    [JsonRequired]
    public Guid BedId { get; set; }

    [JsonRequired]
    public string WardName { get; set; } = string.Empty;

    [JsonRequired]
    public string BedNumber { get; set; } = string.Empty;

    [JsonRequired]
    public bool IsDowngrade { get; set; }

    [JsonRequired]
    public bool RequiresDutyManager { get; set; }

    public IReadOnlyList<string> RulesSatisfied { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Why this bed ranked where it did, in plain sentences, best reason first. The hard rules say
    /// a bed is allowed; these say why it was a good idea, which is the part a nurse can argue
    /// with. Empty is possible for an answer restored from an older run.
    /// </summary>
    public IReadOnlyList<string> FitFactors { get; set; } = Array.Empty<string>();

    public string? Rationale { get; set; }
}
