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

    public string? Rationale { get; set; }
}
