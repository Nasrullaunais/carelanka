using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Common;

public class CurrentPrincipal
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public PrincipalType PrincipalType { get; set; }

    [Required]
    public PrincipalRole Role { get; set; }

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public Guid? PatientId { get; set; }
}
