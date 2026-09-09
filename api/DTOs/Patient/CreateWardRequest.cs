using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Body of POST /api/wards.</summary>
public class CreateWardRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(WardType))]
    public WardType WardType { get; set; }

    /// <summary>A property of the ward. ICU and pediatric are mixed; general wards usually are not.</summary>
    [Required]
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy GenderPolicy { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; } = true;
}
