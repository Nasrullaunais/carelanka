using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class BillLine
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public BillLineSource Source { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [Required]
    public decimal LineTotal { get; set; }
}
