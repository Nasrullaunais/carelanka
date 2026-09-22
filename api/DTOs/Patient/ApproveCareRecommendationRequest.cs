namespace CareLanka.Api.DTOs.Patient;

public sealed class ApproveCareRecommendationRequest
{
    /// <summary>
    /// Leave unset to approve the agent's draft unchanged. Set to override it.
    /// </summary>
    public string? DoctorMessage { get; set; }
}
