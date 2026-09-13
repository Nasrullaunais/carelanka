using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class CreatePharmacyTransactionRequest
{
    [Required]
    public PharmacyTransactionType Type { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}
