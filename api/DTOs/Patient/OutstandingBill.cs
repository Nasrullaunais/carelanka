using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class OutstandingBill
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public PatientSummary Patient { get; set; } = null!;

    [Required]
    public AdmissionStatus Status { get; set; }

    [Required]
    public AdmissionCategory AdmissionCategory { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    public DateTimeOffset? AdmittedAt { get; set; }

    public string? BillNumber { get; set; }

    [Required]
    public bool Settled { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    [Required]
    public decimal EstimatedTotal { get; set; }

    [Required]
    public string Currency { get; set; } = "LKR";
}
