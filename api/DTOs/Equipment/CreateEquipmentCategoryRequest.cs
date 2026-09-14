using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class CreateEquipmentCategoryRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}
