using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class UpdateEquipmentItemRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(150)]
    public string? Manufacturer { get; set; }

    public EquipmentStatus? Status { get; set; }

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
