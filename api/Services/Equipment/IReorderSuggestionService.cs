using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

public interface IReorderSuggestionService
{
    Task<ReorderSuggestionAccepted> SubmitAsync(Guid pharmacyItemId, CancellationToken ct = default);

    Task<ReorderWorkflowSummary> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default);
}
