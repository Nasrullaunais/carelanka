namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Who signed off a care reply. Deliberately narrower than the group-owned StaffRole: the
/// CareRecommendationReviewer policy admits a Doctor or a Ward Nurse and nobody else, so those
/// are the only two a patient can ever be shown.
/// </summary>
public enum CareReviewerRole
{
    Doctor,
    WardNurse
}
