using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

public class EquipmentItem : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public Guid CategoryId { get; set; }

    public EquipmentCategory Category { get; set; } = null!;

    public string Model { get; set; } = null!;

    public string Manufacturer { get; set; } = null!;

    public DateOnly PurchaseDate { get; set; }

    public EquipmentStatus Status { get; set; }

    public Guid? WardId { get; set; }

    public Guid? AssignedToAdmissionId { get; set; }

    public string AssetTag { get; set; } = null!;

    public string? SerialNumber { get; set; }

    public DateOnly? NextMaintenanceDue { get; set; }

    public bool AwaitingConfirmation { get; set; }

    public Guid? ConfirmedByStaffId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
}
