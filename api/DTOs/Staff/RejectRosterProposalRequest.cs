using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class RejectRosterProposalRequest
{
    [Required]
    public RejectionReason Reason { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
