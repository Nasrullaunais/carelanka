using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class AssignEquipmentItemRequest
{
    [Required]
    public Guid AdmissionId { get; set; }
}
