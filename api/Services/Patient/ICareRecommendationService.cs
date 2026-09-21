using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareRecommendationResponse = CareLanka.Api.DTOs.Patient.CareRecommendation;

namespace CareLanka.Api.Services.Patient;

public interface ICareRecommendationService
{
    Task<CareWorkflowAccepted> SubmitAsync(
        Guid patientId, CareQueryRequest request, CancellationToken ct = default);

    Task<CareWorkflowSummary> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Run the agent again for a report that is still awaiting review. Exists because the usual
    /// reason a draft is the fixed backup sentence is a busy model, which clears on its own - so
    /// the reviewer's answer to a useless draft should be "try again", not "approve it anyway".
    /// </summary>
    Task<CareWorkflowAccepted> RedraftAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<CareRecommendationSummary>> ListAsync(
        CareRecommendationStatus? status,
        int page,
        int pageSize,
        DTOs.Patient.SortDirection sortDir,
        CancellationToken ct = default);

    Task<CareRecommendationResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<CareRecommendationResponse> ApproveAsync(
        Guid id, ApproveCareRecommendationRequest? request, Guid reviewerStaffId, CancellationToken ct = default);

    Task<CareRecommendationResponse> RejectAsync(
        Guid id, RejectCareRecommendationRequest request, Guid reviewerStaffId, CancellationToken ct = default);

    Task<PagedResult<MyCareRecommendation>> GetMyRecommendationsAsync(
        Guid patientId, int page, int pageSize, CancellationToken ct = default);
}
