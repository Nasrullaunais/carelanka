using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// What a visit costs. Bed days and an admission fee are generated from what the hospital
/// actually recorded; everything else is a line somebody at the desk typed.
/// </summary>
public class Bill
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    /// <summary>The handle a patient quotes at the counter.</summary>
    [Required]
    public string BillNumber { get; set; } = string.Empty;

    /// <summary>Sri Lankan rupees. Fixed - this component does not do currency conversion.</summary>
    [Required]
    public string Currency { get; set; } = "LKR";

    [Required]
    public IReadOnlyList<BillLine> Lines { get; set; } = Array.Empty<BillLine>();

    /// <summary>The sum of the lines. Not stored, so it cannot disagree with them.</summary>
    [Required]
    public decimal Total { get; set; }

    /// <summary>
    /// True once the money is in. This is the same fact as the <c>billing_settled</c> checklist
    /// item, written once - settling is what ticks the box, and the box cannot be ticked any
    /// other way.
    /// </summary>
    [Required]
    public bool Settled { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid? SettledByStaffId { get; set; }

    /// <summary>Their name, for a screen. Sent beside the id, never instead of it.</summary>
    public string? SettledByStaffName { get; set; }

    public string? SettlementNote { get; set; }

    /// <summary>Who the bill is for, so a printed copy needs no second request.</summary>
    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
