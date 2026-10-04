namespace CareLanka.Api.DTOs.Emergency;

public sealed class AddressSuggestion
{
    public string Label { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal ApproximateAccuracyMetres { get; set; }
}
