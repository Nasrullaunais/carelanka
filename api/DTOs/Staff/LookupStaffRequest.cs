using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public sealed class LookupStaffRequest
{
    [Required]
    [MaxLength(100)]
    public IReadOnlyList<Guid> StaffIds { get; set; } = null!;
}
