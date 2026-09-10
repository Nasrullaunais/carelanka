using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// One row per physical unit. Ventilator #3 is not the same row as Ventilator #4.
public class EquipmentItem : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public EquipmentCategory Category { get; set; } = null!;

    public string Model { get; set; } = null!;

    public string Manufacturer { get; set; } = null!;

    public DateOnly PurchaseDate { get; set; }

    public EquipmentStatus Status { get; set; }

    // Where the item currently sits, or null for the central store. Patient Management's
    // ward, stored as a bare reference: we never write their table.
    public Guid? WardId { get; set; }

    // Set while Status is Assigned, cleared on release. Patient Management's admission, id
    // only. No assignment history is kept, which plan section 4.1 flags as deliberate.
    public Guid? AssignedToAdmissionId { get; set; }

    // Printed as a QR code on the physical item. What the technician scans.
    public string AssetTag { get; set; } = null!;

    // Distinct from the model name, for items where several units share a model.
    public string? SerialNumber { get; set; }

    public DateOnly? NextMaintenanceDue { get; set; }
}
