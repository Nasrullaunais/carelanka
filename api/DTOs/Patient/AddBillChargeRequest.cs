using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class AddBillChargeRequest
{
    [Required]
    [MaxLength(200)]
    [MinLength(1)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 9999.99)]
    public decimal? Quantity { get; set; }

    [Required]
    [Range(0, 9999999.99)]
    public decimal? UnitPrice { get; set; }
}
