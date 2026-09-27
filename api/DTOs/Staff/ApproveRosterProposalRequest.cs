using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class ApproveRosterProposalRequest
{
    [MaxLength(500)]
    public string? Notes { get; set; }
}
