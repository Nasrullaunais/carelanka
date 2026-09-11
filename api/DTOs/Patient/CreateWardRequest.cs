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

    // Both enums below are nullable, which looks like a mistake and is not. [Required] on a plain
    // enum always passes: the model binder has already turned an absent key into the first
    // declared member, so there is nothing left for validation to object to. Nullable is what
    // turns a missing key into a 400 instead of a silent default.

    /// <summary>Required. An omitted key would create an ICU, because `icu` is declared first.</summary>
    [Required]
    [EnumDataType(typeof(WardType))]
    public WardType? WardType { get; set; }

    /// <summary>
    /// A property of the ward. ICU and pediatric are mixed; general wards usually are not.
    /// Required: an omitted key would make the ward male-only, because `male` is declared first.
    /// </summary>
    [Required]
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy? GenderPolicy { get; set; }

    [DefaultValue(true)]
    public bool IsActive { get; set; } = true;
}
