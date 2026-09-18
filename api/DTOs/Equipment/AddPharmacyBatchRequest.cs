using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Equipment;

// A delivery of a medicine already in the catalog: the boxes that arrived and when they expire.
public sealed class AddPharmacyBatchRequest
{
    [JsonRequired]
    public int Quantity { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }
}
