using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class CreateWardRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(WardType))]
    public WardType? WardType { get; set; }

    [Required]
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy? GenderPolicy { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; } = true;
}
