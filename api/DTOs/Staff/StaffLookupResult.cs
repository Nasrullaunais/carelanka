using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public sealed class StaffLookupResult
{
    [Required]
    public Guid StaffId { get; set; }

    [Required]
    public bool Found { get; set; }

    public string? FullName { get; set; }

    public StaffRole? Role { get; set; }

    public bool? IsActive { get; set; }
}
