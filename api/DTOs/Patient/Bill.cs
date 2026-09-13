using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class Bill
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public string BillNumber { get; set; } = string.Empty;

    [Required]
    public string Currency { get; set; } = "LKR";

    [Required]
    public IReadOnlyList<BillLine> Lines { get; set; } = Array.Empty<BillLine>();

    [Required]
    public decimal Total { get; set; }

    [Required]
    public bool Settled { get; set; }

    public Guid? RaisedByStaffId { get; set; }

    public string? RaisedByStaffName { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    public Guid? SettledByStaffId { get; set; }

    public string? SettledByStaffName { get; set; }

    public string? SettlementNote { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
