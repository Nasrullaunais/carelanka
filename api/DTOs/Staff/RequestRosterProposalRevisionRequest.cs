using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class RequestRosterProposalRevisionRequest
{
    [Required]
    [MaxLength(500)]
    public string Guidance { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes
    {
        get => Guidance;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Guidance))
            {
                Guidance = value;
            }
        }
    }

    public IReadOnlyList<Guid> ExcludeStaffIds { get; set; } = [];

    public IReadOnlyList<Guid> ExcludeWardIds { get; set; } = [];
}
