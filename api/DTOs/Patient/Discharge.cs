using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class Discharge
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public AssignedBy FlaggedBy { get; set; }

    [Required]
    public DateTimeOffset FlaggedAt { get; set; }

    [Required]
    public IDictionary<string, ChecklistItem> Checklist { get; set; }
        = new Dictionary<string, ChecklistItem>();

    [Required]
    public bool AllMandatoryTicked { get; set; }

    public Guid? ConfirmedByStaffId { get; set; }

    public string? ConfirmedByStaffName { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public string? SummaryNote { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
