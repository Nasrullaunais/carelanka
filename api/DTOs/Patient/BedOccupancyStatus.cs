using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class BedOccupancyStatus
{
    [Required]
    public Guid BedId { get; set; }

    [Required]
    public bool Occupied { get; set; }

    public AssignmentStatus? AssignmentStatus { get; set; }

    public DateTimeOffset? ReservedUntil { get; set; }

    [Required]
    public bool MayTakeOutOfService { get; set; }
}
