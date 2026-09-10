using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/equipment-items/{id}/assign.</summary>
public class AssignEquipmentItemRequest
{
    /// <summary>Patient Management's admission. Stored as an id; no patient data is copied here.</summary>
    [Required]
    public Guid AdmissionId { get; set; }
}
