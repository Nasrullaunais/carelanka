using CareLanka.Api.Agents.Equipment;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using SuggestionEntity = CareLanka.Api.Data.Entities.Equipment.ReorderSuggestion;

namespace CareLanka.Api.Services.Equipment;

/// <summary>
/// The reorder-threshold advisor's entry point and its record - guards the run, persists the
/// suggestion and the plan before anything executes, hands the work to the background worker,
/// and answers the poll the panel watches. Same shape as
/// <c>CareRecommendationService.SubmitAsync</c>/<c>GetWorkflowAsync</c>, minus the review queue:
/// there is nothing here for a human to approve, only a number to read and, separately, apply.
/// </summary>
public sealed class ReorderSuggestionService : IReorderSuggestionService
{
    public const string Objective = "suggest_reorder_threshold";

    private readonly CareLankaDbContext _db;
    private readonly IReorderRunQueue _queue;

    public ReorderSuggestionService(CareLankaDbContext db, IReorderRunQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<ReorderSuggestionAccepted> SubmitAsync(
        Guid pharmacyItemId, CancellationToken ct = default)
    {
        var item = await _db.PharmacyItems
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == pharmacyItemId, ct)
            ?? throw new NotFoundException("Pharmacy item", pharmacyItemId);

        var alreadyRunning = await (
            from existingWorkflow in _db.AgentWorkflows
            join existingSuggestion in _db.ReorderSuggestions
                on existingWorkflow.EntityId equals existingSuggestion.Id
            where existingWorkflow.AgentType == AgentType.EquipmentMonitoring
                && existingWorkflow.EntityType == ReorderAgentExecutor.WorkflowEntityType
                && existingWorkflow.Status == AgentWorkflowStatus.Pending
                && existingSuggestion.PharmacyItemId == pharmacyItemId
            select existingWorkflow.Id
        ).AnyAsync(ct);

        if (alreadyRunning)
        {
            throw new ConflictException(MessageCode.ReorderSuggestionAlreadyRunning, item.Name);
        }

        var suggestionRow = new SuggestionEntity
        {
            Id = Guid.NewGuid(),
            PharmacyItemId = item.Id,
            CurrentThreshold = item.ReorderThreshold,
            CurrentQuantityOnHand = item.QuantityOnHand
        };

        _db.ReorderSuggestions.Add(suggestionRow);

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.EquipmentMonitoring,
            EntityType = ReorderAgentExecutor.WorkflowEntityType,
            EntityId = suggestionRow.Id,
            CorrelationId = Guid.NewGuid(),
            Objective = Objective,
            Plan = BedWorkflowJson.Write(ReorderAgent.Plan),
            Status = AgentWorkflowStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        };

        _db.AgentWorkflows.Add(workflow);

        // Both rows are on disk before a single tool runs, so a run that dies mid-flight leaves a
        // trace rather than nothing, and the caller gets a suggestion_id immediately.
        await _db.SaveChangesAsync(ct);

        _queue.Enqueue(workflow.Id);

        return new ReorderSuggestionAccepted
        {
            WorkflowId = workflow.Id,
            SuggestionId = suggestionRow.Id,
            Status = "running",
            PollUrl = $"/api/pharmacy-items/reorder-suggestions/{workflow.Id}"
        };
    }

    public async Task<ReorderWorkflowSummary> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == workflowId && row.AgentType == AgentType.EquipmentMonitoring, ct)
            ?? throw new NotFoundException("AgentWorkflow", workflowId);

        var suggestion = await _db.ReorderSuggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == workflow.EntityId, ct)
            ?? throw new NotFoundException("ReorderSuggestion", workflow.EntityId);

        var validation = BedWorkflowJson.Read<ReorderWorkflowValidationRecord>(workflow.ValidationResults);

        return new ReorderWorkflowSummary
        {
            WorkflowId = workflow.Id,
            SuggestionId = suggestion.Id,
            PharmacyItemId = suggestion.PharmacyItemId,
            Objective = workflow.Objective,
            Status = Status(workflow),
            Plan = BedWorkflowJson.Read<List<string>>(workflow.Plan) ?? [],
            Steps = BedWorkflowJson.Read<List<BedAgentStep>>(workflow.CompletedSteps) ?? [],
            CurrentThreshold = suggestion.CurrentThreshold,
            CurrentQuantityOnHand = suggestion.CurrentQuantityOnHand,
            SuggestedThreshold = suggestion.SuggestedThreshold,
            Reasoning = suggestion.Reasoning,
            Source = suggestion.Source,
            Retries = Math.Max(0, workflow.AttemptCount - 1)
        };
    }

    private static ReorderWorkflowStatus Status(AgentWorkflow workflow)
    {
        if (workflow.Status == AgentWorkflowStatus.Failed)
        {
            return ReorderWorkflowStatus.Failed;
        }

        return workflow.CompletedAt is null ? ReorderWorkflowStatus.Running : ReorderWorkflowStatus.Completed;
    }
}
