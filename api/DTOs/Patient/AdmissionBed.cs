using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class AdmissionBed
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string WardName { get; set; } = null!;

    [Required]
    public string BedNumber { get; set; } = null!;

    [Required]
    public bool HasIsolation { get; set; }

    [Required]
    public BedCondition Condition { get; set; }

    [Required]
    public BedAvailability Availability { get; set; }

    public Guid? OccupiedByAdmissionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
