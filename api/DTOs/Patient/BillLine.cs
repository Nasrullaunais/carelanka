using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>One line on a bill.</summary>
public class BillLine
{
    [Required]
    public Guid Id { get; set; }

    /// <summary>Generated lines are replaced every time the bill is prepared; only a <c>manual</c> line can be removed.</summary>
    [Required]
    public BillLineSource Source { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity x unit price. Computed, never stored.</summary>
    [Required]
    public decimal LineTotal { get; set; }
}
