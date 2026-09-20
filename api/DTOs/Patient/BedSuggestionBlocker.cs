using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Null when nothing is in the way. The sentence is built in C# from the rule that actually
/// stopped the run, never by the model - a model asked to explain its own failure writes
/// something plausible rather than something true.
/// </summary>
public sealed class BedSuggestionBlocker
{
    [JsonRequired]
    public BedSuggestionBlockerCode Code { get; set; }

    [JsonRequired]
    public string Message { get; set; } = string.Empty;
}
