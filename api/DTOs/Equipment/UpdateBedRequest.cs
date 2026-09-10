using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of PATCH /api/beds/{id}. Every field is optional; an absent field is left alone.</summary>
public class UpdateBedRequest
{
    public bool? HasIsolation { get; set; }

    /// <summary>1 is closest to the nurse station.</summary>
    [Range(1, int.MaxValue)]
    public int? NurseStationDistance { get; set; }

    public Data.Enums.BedCondition? Condition { get; set; }

    // asset_tag is the one field where null is a real value rather than "not supplied":
    // the contract allows clearing a tag. The deserializer only calls a setter for a
    // property that is actually present in the body, so recording the call is how we
    // tell "clear this" from "leave it alone". A plain string? cannot express both.
    private string? _assetTag;

    [MaxLength(50)]
    public string? AssetTag
    {
        get => _assetTag;
        set
        {
            _assetTag = value;
            AssetTagSupplied = true;
        }
    }

    [JsonIgnore]
    public bool AssetTagSupplied { get; private set; }
}
