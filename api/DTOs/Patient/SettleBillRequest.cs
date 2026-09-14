using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class SettleBillRequest
{
    [MaxLength(300)]
    public string? SettlementNote { get; set; }
}
