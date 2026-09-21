using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public sealed class CareRecommendationSummary
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public DateTimeOffset ReportedAt { get; set; }

    public bool RedFlag { get; set; }

    public CareUrgency? UrgencyFlag { get; set; }

    public CareRecommendationStatus Status { get; set; }
}
