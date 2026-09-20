using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IBedSuggestionService
{
    Task<BedWorkflowAccepted> StartAsync(
        BedSuggestionRequest request, CancellationToken cancellationToken = default);

    Task<BedWorkflowSummary> GetAsync(
        Guid workflowId, CancellationToken cancellationToken = default);
}
