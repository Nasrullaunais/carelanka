namespace CareLanka.Api.DTOs.Patient;

public sealed class RejectCareRecommendationRequest
{
    /// <summary>
    /// Staff-facing only. Never sent to the patient.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
