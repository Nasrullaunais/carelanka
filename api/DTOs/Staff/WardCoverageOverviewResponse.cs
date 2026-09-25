using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class WardCoverageOverviewResponse
{
    [Required]
    public DateTimeOffset GeneratedAt { get; set; }

    [Required]
    public IReadOnlyList<WardCoverageDto> Wards { get; set; } = Array.Empty<WardCoverageDto>();
}
