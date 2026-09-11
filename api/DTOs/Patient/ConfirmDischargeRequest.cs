using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>What a nurse writes on the way out.</summary>
public class ConfirmDischargeRequest
{
    /// <summary>Instructions the patient can read on their own phone afterwards.</summary>
    [MaxLength(2000)]
    public string? SummaryNote { get; set; }
}
