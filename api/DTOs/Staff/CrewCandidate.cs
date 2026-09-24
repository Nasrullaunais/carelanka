using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public sealed class CrewCandidate
{
    [Required]
    public Guid StaffMemberId { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;
}
