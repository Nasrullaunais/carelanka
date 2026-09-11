using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// A charge reception types in - an X-ray, a dressing pack, a consultant's fee.
/// </summary>
/// <remarks>
/// Typed rather than generated because nothing in this component records a treatment, a
/// procedure or a drug against an admission. Inventing line items from tables that do not exist
/// would be worse than asking a human to type what actually happened.
/// </remarks>
public class AddBillChargeRequest
{
    [Required]
    [MaxLength(200)]
    [MinLength(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Nullable so that leaving it out is a 400 rather than a silent zero - the same trap as
    /// every required value type in this component.
    /// </summary>
    [Required]
    [Range(0.01, 9999.99)]
    public decimal? Quantity { get; set; }

    [Required]
    [Range(0, 9999999.99)]
    public decimal? UnitPrice { get; set; }
}
