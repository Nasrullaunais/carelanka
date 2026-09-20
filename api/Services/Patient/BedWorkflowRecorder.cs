using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IBedWorkflowRecorder
{
    void Record(AgentWorkflow workflow, BedAgentRun run, Guid? admissionId);

    void RecordFailure(AgentWorkflow workflow, Exception failure);
}

/// <summary>
/// Writes what a run did onto its workflow row: plan, steps with timings, tool calls, validation,
/// errors, retries, the approver it needs and the outcome. One place, because the request path and
/// the background worker both finish runs and a row written two different ways is a row you cannot
/// read with confidence.
/// </summary>
public sealed class BedWorkflowRecorder : IBedWorkflowRecorder
{
    public const string WorkflowEntityType = "Admission";

    private readonly CareLankaDbContext _db;

    public BedWorkflowRecorder(CareLankaDbContext db) => _db = db;

    public void Record(AgentWorkflow workflow, BedAgentRun run, Guid? admissionId)
    {
        var resolved = run.AdmissionId ?? admissionId;

        if (resolved is { } id && id != Guid.Empty)
        {
            workflow.EntityId = id;
        }

        workflow.CompletedSteps = BedWorkflowJson.Write(run.Steps);
        workflow.ToolResults = BedWorkflowJson.Write(run.ToolCalls);
        workflow.ValidationResults = BedWorkflowJson.Write(new PersistedValidation
        {
            Passed = run.Validation.Passed,
            FailedRules = run.Validation.FailedRules,
            Blocker = run.Answer.Blocker
        });
        workflow.Errors = run.Errors.Count == 0 ? null : BedWorkflowJson.Write(run.Errors);
        workflow.FinalOutcome = EnumWire.ToWire(run.Answer.Outcome);
        workflow.RequiredApproverRole = ApproverRole(run.Answer.RequiresApprovalBy);
        workflow.AttemptCount = run.Attempts;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
        workflow.Status = Status(run.Answer.Outcome);

        var sequence = 1;

        foreach (var bed in new[] { run.Answer.Best }
                     .Concat(run.Answer.Alternatives)
                     .OfType<RankedBed>())
        {
            _db.AgentProposedChanges.Add(
                ToProposedChange(workflow, bed, sequence++, workflow.EntityId));
        }
    }

    /// <summary>
    /// The run died somewhere the agent's own safe failure could not catch it. The row still ends
    /// as a finished, readable answer rather than sitting at pending for good.
    /// </summary>
    public void RecordFailure(AgentWorkflow workflow, Exception failure)
    {
        workflow.ValidationResults = BedWorkflowJson.Write(new PersistedValidation
        {
            Passed = false,
            FailedRules = Array.Empty<string>(),
            Blocker = BedBlockers.AgentFailed()
        });
        workflow.Errors = BedWorkflowJson.Write(new[] { failure.Message });
        workflow.FinalOutcome = EnumWire.ToWire(BedAgentOutcome.Failed);
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static AgentProposedChange ToProposedChange(
        AgentWorkflow workflow, RankedBed bed, int sequence, Guid admissionId)
        => new()
        {
            Id = Guid.NewGuid(),
            AgentWorkflowId = workflow.Id,
            Sequence = sequence,
            ChangeType = ProposedChangeType.ReserveBed,
            TargetEntityType = WorkflowEntityType,
            TargetEntityId = admissionId == Guid.Empty ? null : admissionId,
            ProposedBedId = bed.Candidate.Bed.Id,
            ProposedWardId = bed.Candidate.Ward.Id,
            Payload = BedWorkflowJson.Write(new PersistedBed
            {
                WardName = bed.Candidate.Ward.Name,
                BedNumber = bed.Candidate.Bed.BedNumber,
                IsDowngrade = bed.IsDowngrade,
                RequiresDutyManager = bed.RequiresDutyManager,
                RulesSatisfied = BedRuleNames.Satisfied(bed.IsDowngrade),
                Rationale = bed.Rationale
            }),
            ValidationStatus = ProposedChangeValidationStatus.Passed
        };

    /// <summary>
    /// A proposal waits for a human. Every other outcome is a finished answer needing nobody's
    /// approval, so it is recorded as executed rather than left looking like an open decision.
    /// </summary>
    private static AgentWorkflowStatus Status(BedAgentOutcome outcome) => outcome switch
    {
        BedAgentOutcome.Proposed or BedAgentOutcome.ProposedWithDowngrade
            => AgentWorkflowStatus.PendingApproval,
        BedAgentOutcome.Failed => AgentWorkflowStatus.Failed,
        _ => AgentWorkflowStatus.Executed
    };

    private static StaffRole? ApproverRole(BedApproverRole? approver) => approver switch
    {
        BedApproverRole.WardNurse => StaffRole.WardNurse,
        BedApproverRole.DutyManager => StaffRole.DutyManager,
        _ => null
    };
}

public sealed class PersistedValidation
{
    public bool Passed { get; set; } = true;

    public IReadOnlyList<string> FailedRules { get; set; } = Array.Empty<string>();

    public BedSuggestionBlocker? Blocker { get; set; }
}

public sealed class PersistedBed
{
    public string WardName { get; set; } = string.Empty;

    public string BedNumber { get; set; } = string.Empty;

    public bool IsDowngrade { get; set; }

    public bool RequiresDutyManager { get; set; }

    public IReadOnlyList<string> RulesSatisfied { get; set; } = Array.Empty<string>();

    public string? Rationale { get; set; }
}
