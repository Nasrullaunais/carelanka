using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Equipment;

public class UpdateBedRequest
{
    public bool? HasIsolation { get; set; }

    [Range(1, int.MaxValue)]
    public int? NurseStationDistance { get; set; }

    public Data.Enums.BedCondition? Condition { get; set; }

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
