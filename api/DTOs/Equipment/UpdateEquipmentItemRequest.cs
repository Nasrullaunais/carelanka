using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of PUT /api/equipment-items/{id}. Every field is optional; an absent field is left alone.</summary>
public class UpdateEquipmentItemRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(150)]
    public string? Manufacturer { get; set; }

    public EquipmentStatus? Status { get; set; }

    // ward_id and next_maintenance_due are both nullable in the contract, so null is a real
    // value meaning "move it to the central store" and "no service booked". The deserializer
    // only calls a setter for a property present in the body, so recording the call is how
    // we tell that from "leave it alone". Same pattern as UpdateBedRequest.AssetTag.
    private Guid? _wardId;

    public Guid? WardId
    {
        get => _wardId;
        set { _wardId = value; WardIdSupplied = true; }
    }

    [JsonIgnore]
    public bool WardIdSupplied { get; private set; }

    private DateOnly? _nextMaintenanceDue;

    public DateOnly? NextMaintenanceDue
    {
        get => _nextMaintenanceDue;
        set { _nextMaintenanceDue = value; NextMaintenanceDueSupplied = true; }
    }

    [JsonIgnore]
    public bool NextMaintenanceDueSupplied { get; private set; }
}
