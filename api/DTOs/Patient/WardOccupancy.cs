using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class WardOccupancy
{
    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public WardType WardType { get; set; }

    [Required]
    public int TotalBeds { get; set; }

    [Required]
    public int OccupiedBeds { get; set; }

    [Required]
    public int ReservedBeds { get; set; }

    [Required]
    public int OutOfServiceBeds { get; set; }

    [Required]
    public IReadOnlyDictionary<string, int> PatientsByCategory { get; set; } =
        new Dictionary<string, int>();

    [Required]
    [JsonPropertyName("incoming_next_2h")]
    public int IncomingNext2h { get; set; }
}
