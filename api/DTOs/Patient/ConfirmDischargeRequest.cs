using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class ConfirmDischargeRequest
{
    [MaxLength(2000)]
    public string? SummaryNote { get; set; }
}
