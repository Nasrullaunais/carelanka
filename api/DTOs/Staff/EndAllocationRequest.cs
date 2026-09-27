using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class EndAllocationRequest
{
    [Required]
    public AllocationEndReason Reason { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool SuppressAgent { get; set; } = false;
}