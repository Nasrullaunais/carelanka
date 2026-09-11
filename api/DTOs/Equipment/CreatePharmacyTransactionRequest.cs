using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/pharmacy-items/{id}/transactions.</summary>
public class CreatePharmacyTransactionRequest
{
    [Required]
    public PharmacyTransactionType Type { get; set; }

    /// <summary>Always positive. Type decides whether stock goes up or down.</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    /// <summary>Mandatory for an adjustment. A stocktake correction nobody explained is unauditable.</summary>
    [MaxLength(300)]
    public string? Note { get; set; }
}
