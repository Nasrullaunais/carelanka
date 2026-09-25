using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using CareRecommendationEntity = CareLanka.Api.Data.Entities.Patient.CareRecommendation;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Runs one queued care-agent workflow to completion and writes the result onto its workflow row
/// and its <see cref="CareRecommendationEntity"/> row. A run that throws still ends as a row
/// marked failed, never left pending forever.
/// </summary>
public sealed class CareAgentExecutor
{
    public const string WorkflowEntityType = "CareRecommendation";

    private readonly CareLankaDbContext _db;
    private readonly ICareAgent _agent;
    private readonly ILogger<CareAgentExecutor> _log;

    public CareAgentExecutor(CareLankaDbContext db, ICareAgent agent, ILogger<CareAgentExecutor> log)
    {
        _db = db;
        _agent = agent;
        _log = log;
    }

    public async Task ExecuteAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .FirstOrDefaultAsync(
                row => row.Id == workflowId && row.AgentType == AgentType.PatientCareAdvisory, ct);

        if (workflow is null)
        {
            _log.LogWarning("Care agent workflow {WorkflowId} is gone; nothing to run.", workflowId);

            return;
        }

        if (workflow.Status != AgentWorkflowStatus.Pending)
        {
            // Already run. Re-running would overwrite a draft a reviewer may already be reading.
            return;
        }

        var recommendation = await _db.CareRecommendations
            .FirstOrDefaultAsync(row => row.Id == workflow.EntityId, ct);

        if (recommendation is null)
        {
            _log.LogWarning(
                "Care recommendation {RecommendationId} for workflow {WorkflowId} is gone.",
                workflow.EntityId, workflowId);

            RecordFailure(workflow);
            await _db.SaveChangesAsync(ct);

            return;
        }

        if (recommendation.Status != CareRecommendationStatus.PendingReview)
        {
            RecordAlreadyReviewed(workflow);
            await _db.SaveChangesAsync(ct);

            return;
        }

        try
        {
            var run = await _agent.RunAsync(
                new CareAgentRequest(
                    recommendation.PatientId,
                    recommendation.AdmissionId ?? Guid.Empty,
                    recommendation.ReportedText,
                    steps => SaveProgressAsync(workflow, steps, ct)),
                ct);

            // A reviewer may have acted while the model was answering, once the run outlived
            // CareRecommendationService.RunGivenUpAfter. Their decision stands over a late draft.
            await _db.Entry(recommendation).ReloadAsync(ct);

            if (recommendation.Status != CareRecommendationStatus.PendingReview)
            {
                RecordAlreadyReviewed(workflow);
            }
            else
            {
                Record(workflow, recommendation, run);
            }
        }
        catch (Exception failure)
        {
            _log.LogError(failure, "Care agent workflow {WorkflowId} failed.", workflowId);

            RecordFailure(workflow);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveProgressAsync(
        AgentWorkflow workflow, IReadOnlyList<CareAgentStep> steps, CancellationToken ct)
    {
        workflow.CompletedSteps = CareWorkflowJson.Write(steps);
        await _db.SaveChangesAsync(ct);
    }

    private void Record(AgentWorkflow workflow, CareRecommendationEntity recommendation, CareAgentRun run)
    {
        workflow.CompletedSteps = CareWorkflowJson.Write(run.Steps);
        workflow.ValidationResults = CareWorkflowJson.Write(new CareWorkflowValidationRecord
        {
            Passed = run.Validation.Passed,
            FailedRules = run.Validation.FailedRules,
            DraftSource = EnumWire.ToWire(run.Draft?.Source ?? DTOs.Patient.CareDraftSource.Model),
            DraftNote = run.Draft?.SourceNote
        });
        workflow.Errors = run.Errors.Count == 0 ? null : CareWorkflowJson.Write(run.Errors);
        workflow.FinalOutcome = EnumWire.ToWire(run.Outcome);
        workflow.AttemptCount = run.Attempts;
        workflow.CompletedAt = DateTimeOffset.UtcNow;

        recommendation.RedFlag = run.RedFlag;

        if (run.Draft is { } draft)
        {
            recommendation.UrgencyFlag = draft.UrgencyFlag;
            recommendation.AgentMessage = draft.Message;
            workflow.Status = AgentWorkflowStatus.PendingApproval;
            workflow.RequiredApproverRole = null; // Either WardNurse or Doctor - not one role.

            _db.AgentProposedChanges.Add(new AgentProposedChange
            {
                Id = Guid.NewGuid(),
                AgentWorkflowId = workflow.Id,
                Sequence = 1,
                ChangeType = ProposedChangeType.CreateCareRecommendation,
                TargetEntityType = WorkflowEntityType,
                TargetEntityId = recommendation.Id,
                Payload = CareWorkflowJson.Write(new
                {
                    patient_id = recommendation.PatientId,
                    admission_id = recommendation.AdmissionId,
                    urgency_flag = EnumWire.ToWire(draft.UrgencyFlag),
                    agent_message = draft.Message
                }),
                ValidationStatus = ProposedChangeValidationStatus.Passed,
                AppliedAt = DateTimeOffset.UtcNow,
                AppliedEntityId = recommendation.Id
            });
        }
        else
        {
            // Every attempt failed. The recommendation stays pending_review with no draft, so a
            // doctor or nurse can still act on the patient's own report directly.
            workflow.Status = AgentWorkflowStatus.Failed;
        }
    }

    private static void RecordAlreadyReviewed(AgentWorkflow workflow)
    {
        workflow.ValidationResults = CareWorkflowJson.Write(
            new CareWorkflowValidationRecord { Passed = false, FailedRules = Array.Empty<string>() });
        workflow.Errors = CareWorkflowJson.Write(
            new[] { "A nurse or doctor reviewed this report before the agent finished, so its draft was not used." });
        workflow.FinalOutcome = EnumWire.ToWire(DTOs.Patient.CareAgentOutcome.Failed);
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static void RecordFailure(AgentWorkflow workflow)
    {
        workflow.ValidationResults = CareWorkflowJson.Write(
            new CareWorkflowValidationRecord { Passed = false, FailedRules = Array.Empty<string>() });
        workflow.Errors = CareWorkflowJson.Write(new[] { "The care agent could not complete this run." });
        workflow.FinalOutcome = EnumWire.ToWire(DTOs.Patient.CareAgentOutcome.Failed);
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Persisted as JSON in <c>agent_workflows.validation_results</c>. The two draft fields were added
/// after the first rows were written, so a row without them reads back as a model draft with no
/// note - which is what those rows were.
/// </summary>
public sealed class CareWorkflowValidationRecord
{
    public bool Passed { get; set; } = true;

    public IReadOnlyList<string> FailedRules { get; set; } = Array.Empty<string>();

    public string DraftSource { get; set; } = "model";

    public string? DraftNote { get; set; }
}
