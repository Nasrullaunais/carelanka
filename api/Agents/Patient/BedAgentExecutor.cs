using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Runs one bed-agent workflow and writes down what happened. Everything it persists is workflow
/// state - the plan, the steps, the tool calls, the verdicts, the answer. It writes nothing to the
/// domain, because the agent produced nothing to write.
/// </summary>
public sealed class BedAgentExecutor
{
    private readonly CareLankaDbContext _db;
    private readonly BedAgent _agent;
    private readonly TimeProvider _time;
    private readonly ILogger<BedAgentExecutor> _log;

    public BedAgentExecutor(
        CareLankaDbContext db, BedAgent agent, TimeProvider time, ILogger<BedAgentExecutor> log)
    {
        _db = db;
        _agent = agent;
        _time = time;
        _log = log;
    }

    public async Task ExecuteAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .FirstOrDefaultAsync(candidate => candidate.Id == workflowId, ct);

        var suggestion = await _db.BedSuggestions
            .Include(candidate => candidate.Candidates)
            .FirstOrDefaultAsync(candidate => candidate.WorkflowId == workflowId, ct);

        if (workflow is null || suggestion is null)
        {
            _log.LogWarning("Bed agent workflow {WorkflowId} no longer exists.", workflowId);
            return;
        }

        if (workflow.Status != AgentWorkflowStatus.Running)
        {
            // Already run. The queue is at-least-once and a second delivery must not produce a
            // second set of candidate rows against the same unique rank.
            return;
        }

        workflow.StartedAt = _time.GetUtcNow();
        workflow.AttemptCount += 1;

        try
        {
            var result = await _agent.RunAsync(
                suggestion.AdmissionId, suggestion.RequestedIdentifier, ct);

            Persist(workflow, suggestion, result);
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "Bed agent workflow {WorkflowId} failed.", workflowId);

            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.Errors = [.. workflow.Errors, exception.Message];
            workflow.FinalOutcome = Wire(BedAgentOutcome.Failed);
            workflow.CompletedAt = _time.GetUtcNow();

            suggestion.Outcome = BedAgentOutcome.Failed;

            var blocker = BedSuggestionBlockers.AgentFailed();
            suggestion.BlockerCode = blocker.Code;
            suggestion.BlockerMessage = blocker.Message;
        }

        await _db.SaveChangesAsync(ct);
    }

    private void Persist(AgentWorkflow workflow, BedSuggestion suggestion, BedAgentResult result)
    {
        var trace = result.Trace;

        workflow.CompletedSteps = [.. trace.Steps];
        workflow.ToolResults = [.. trace.ToolCalls];
        workflow.ValidationResults = [.. trace.Validations];
        workflow.Errors = [.. trace.Errors];
        workflow.RequiredApproverRole = result.RequiredApproverRole;
        workflow.FinalOutcome = Wire(result.Outcome);
        workflow.CompletedAt = _time.GetUtcNow();

        // A run that suggested something pauses at awaiting_approval and waits for a human. A run
        // that could not is finished: there is nothing for anybody to approve, and leaving it
        // waiting would put an entry in a queue that no button can ever clear.
        workflow.Status = result.Outcome switch
        {
            BedAgentOutcome.Proposed or BedAgentOutcome.ProposedWithDowngrade
                => AgentWorkflowStatus.AwaitingApproval,
            BedAgentOutcome.Failed => AgentWorkflowStatus.Failed,
            _ => AgentWorkflowStatus.Completed
        };

        suggestion.Outcome = result.Outcome;
        suggestion.PatientId = result.PatientId;
        suggestion.AdmissionId = result.AdmissionId ?? suggestion.AdmissionId;
        suggestion.BlockerCode = result.Blocker?.Code;
        suggestion.BlockerMessage = result.Blocker?.Message;
        suggestion.ValidationPassed = trace.AllValidationsPassed;

        var rank = 1;

        foreach (var bed in result.Ranked)
        {
            var candidate = new BedSuggestionCandidate
            {
                Id = Guid.NewGuid(),
                BedSuggestionId = suggestion.Id,
                BedId = bed.Bed.BedId,
                WardId = bed.Bed.WardId,
                Rank = rank++,
                IsDowngrade = bed.IsDowngrade,
                RequiresDutyManager = bed.RequiresDutyManager,
                RulesSatisfied = [.. bed.RulesSatisfied],
                Rationale = bed.Rationale
            };

            // Added to the set as well as the collection: change tracking reads a non-default key
            // on a child it met through a navigation as "this row already exists" and saves it as
            // an UPDATE that affects nothing.
            _db.BedSuggestionCandidates.Add(candidate);
            suggestion.Candidates.Add(candidate);
        }
    }

    private static string Wire(BedAgentOutcome outcome) => EnumWire.ToWire(outcome);
}
