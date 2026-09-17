using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class PendingEquipmentCount
{
    [Required]
    public int Count { get; set; }
}
