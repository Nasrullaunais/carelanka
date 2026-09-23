using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using ReorderSuggestionEntity = CareLanka.Api.Data.Entities.Equipment.ReorderSuggestion;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// Runs one queued reorder-suggestion workflow to completion and writes the result onto its
/// workflow row and its <see cref="ReorderSuggestionEntity"/> row. Same shape as
/// <c>CareAgentExecutor</c>: a run that throws still ends as a row marked failed, never left
/// pending forever. No <c>AgentProposedChange</c> here - a suggestion waits for nobody's
/// approval, it just sits there until a human reads it and decides, the same way the bed agent's
/// pick needs no approval step of its own.
/// </summary>
public sealed class ReorderAgentExecutor
{
    public const string WorkflowEntityType = "ReorderSuggestion";

    private readonly CareLankaDbContext _db;
    private readonly IReorderAgent _agent;
    private readonly ILogger<ReorderAgentExecutor> _log;

    public ReorderAgentExecutor(
        CareLankaDbContext db, IReorderAgent agent, ILogger<ReorderAgentExecutor> log)
    {
        _db = db;
        _agent = agent;
        _log = log;
    }

    public async Task ExecuteAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .FirstOrDefaultAsync(
                row => row.Id == workflowId && row.AgentType == AgentType.EquipmentMonitoring, ct);

        if (workflow is null)
        {
            _log.LogWarning("Reorder agent workflow {WorkflowId} is gone; nothing to run.", workflowId);

            return;
        }

        if (workflow.Status != AgentWorkflowStatus.Pending)
        {
            // Already run. Re-running would overwrite a suggestion a reviewer may already be reading.
            return;
        }

        var suggestion = await _db.ReorderSuggestions
            .FirstOrDefaultAsync(row => row.Id == workflow.EntityId, ct);

        if (suggestion is null)
        {
            _log.LogWarning(
                "Reorder suggestion {SuggestionId} for workflow {WorkflowId} is gone.",
                workflow.EntityId, workflowId);

            RecordFailure(workflow);
            await _db.SaveChangesAsync(ct);

            return;
        }

        try
        {
            var run = await _agent.RunAsync(new ReorderAgentRequest(suggestion.PharmacyItemId), ct);

            Record(workflow, suggestion, run);
        }
        catch (Exception failure)
        {
            _log.LogError(failure, "Reorder agent workflow {WorkflowId} failed.", workflowId);

            RecordFailure(workflow);
        }

        await _db.SaveChangesAsync(ct);
    }

    private void Record(AgentWorkflow workflow, ReorderSuggestionEntity suggestion, ReorderAgentRun run)
    {
        workflow.CompletedSteps = BedWorkflowJson.Write(run.Steps);
        workflow.ValidationResults = BedWorkflowJson.Write(new ReorderWorkflowValidationRecord
        {
            Passed = run.ValidationPassed,
            FailedRule = run.FailedRule
        });
        workflow.Errors = run.Errors.Count == 0 ? null : BedWorkflowJson.Write(run.Errors);
        workflow.FinalOutcome = EnumWire.ToWire(run.Outcome);
        workflow.AttemptCount = run.Attempts;
        workflow.CompletedAt = DateTimeOffset.UtcNow;

        if (run.Draft is { } draft)
        {
            suggestion.SuggestedThreshold = draft.SuggestedThreshold;
            suggestion.Reasoning = draft.Reasoning;
            suggestion.Source = draft.Source;
            suggestion.CompletedAt = DateTimeOffset.UtcNow;

            // A finished answer needing nobody's approval - same reasoning as the bed agent's own
            // suggestion, which is recorded as executed rather than left looking like an open
            // decision waiting on a role that was never going to review it.
            workflow.Status = AgentWorkflowStatus.Executed;
        }
        else
        {
            // Every attempt failed. The item's threshold can still be edited by hand, exactly as
            // it could before this agent existed.
            workflow.Status = AgentWorkflowStatus.Failed;
        }
    }

    private static void RecordFailure(AgentWorkflow workflow)
    {
        workflow.ValidationResults = BedWorkflowJson.Write(
            new ReorderWorkflowValidationRecord { Passed = false });
        workflow.Errors = BedWorkflowJson.Write(new[] { "The reorder agent could not complete this run." });
        workflow.FinalOutcome = EnumWire.ToWire(DTOs.Equipment.ReorderAgentOutcome.Failed);
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Persisted as JSON in <c>agent_workflows.validation_results</c>.
/// </summary>
public sealed class ReorderWorkflowValidationRecord
{
    public bool Passed { get; set; } = true;

    public string? FailedRule { get; set; }
}
