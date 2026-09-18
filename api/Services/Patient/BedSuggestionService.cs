using CareLanka.Api.Agents;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using BedAgentRun = CareLanka.Api.Agents.Patient.BedAgent;
using BedSuggestionEntity = CareLanka.Api.Data.Entities.Patient.BedSuggestion;

namespace CareLanka.Api.Services.Patient;

public sealed class BedSuggestionService : IBedSuggestionService
{
    /// <summary>
    /// The polymorphic target of the workflow row is the suggestion, not the admission.
    /// </summary>
    /// <remarks>
    /// A run started from an NIC has no admission yet, and <c>AgentWorkflow.EntityId</c> is
    /// non-null. Pointing at the suggestion is stable for the life of the row, where pointing at
    /// the admission would mean writing a placeholder and rewriting it mid-run. The admission is
    /// one join away, on the suggestion. Worth confirming with the group when they build the real
    /// table - see STUBS.md.
    /// </remarks>
    public const string WorkflowEntityType = nameof(BedSuggestion);

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly IAgentRunQueue _queue;

    public BedSuggestionService(
        CareLankaDbContext db, IBedRegistryService beds, IAgentRunQueue queue)
    {
        _db = db;
        _beds = beds;
        _queue = queue;
    }

    public async Task<BedWorkflowAccepted> StartAsync(
        BedSuggestionRequest request, CancellationToken ct = default)
    {
        // Checked before a row is written, so a visit that cannot receive a suggestion never costs
        // a workflow record or a model call.
        if (request.AdmissionId is { } admissionId)
        {
            await EnsureCanReceiveSuggestionAsync(admissionId, ct);
        }

        var suggestion = new BedSuggestionEntity
        {
            Id = Guid.NewGuid(),
            AdmissionId = request.AdmissionId,
            RequestedIdentifier = request.PatientIdentifier?.Trim()
        };

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.PatientAdmissionBed,
            EntityType = WorkflowEntityType,
            EntityId = suggestion.Id,
            CorrelationId = Guid.NewGuid(),
            Objective = WorkflowObjective.AssignBed,
            Status = AgentWorkflowStatus.Running,

            // The plan is persisted before anything runs, which is what assignment §9.1 asks for by
            // name - a plan written afterwards is a description of what happened, not a plan.
            Plan = [.. BedAgentRun.Plan]
        };

        suggestion.WorkflowId = workflow.Id;

        _db.AgentWorkflows.Add(workflow);
        _db.BedSuggestions.Add(suggestion);

        await _db.SaveChangesAsync(ct);

        _queue.Enqueue(workflow.Id);

        return new BedWorkflowAccepted
        {
            WorkflowId = workflow.Id,
            AdmissionId = suggestion.AdmissionId,
            Status = EnumWire.ToWire(AgentWorkflowStatus.Running),
            PollUrl = $"/api/bed-workflows/{workflow.Id}"
        };
    }

    public async Task<BedWorkflowSummary> GetAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == workflowId, ct)
            ?? throw new NotFoundException("AgentWorkflow", workflowId);

        var suggestion = await _db.BedSuggestions
            .AsNoTracking()
            .Include(candidate => candidate.Candidates)
            .Include(candidate => candidate.Patient)
            .Include(candidate => candidate.Admission)
            .FirstOrDefaultAsync(candidate => candidate.WorkflowId == workflowId, ct)
            ?? throw new NotFoundException("AgentWorkflow", workflowId);

        var beds = await ToSuggestedBedsAsync(suggestion, ct);

        return new BedWorkflowSummary
        {
            WorkflowId = workflow.Id,
            AdmissionId = suggestion.AdmissionId,
            Objective = workflow.Objective,
            Status = workflow.Status,
            Outcome = suggestion.Outcome,
            Plan = [.. workflow.Plan],
            Steps = workflow.CompletedSteps
                .Select(step => new BedWorkflowStep
                {
                    Step = step.Step,
                    Tool = step.Tool,
                    StartedAt = step.StartedAt,
                    DurationMs = step.DurationMs,
                    Ok = step.Ok,
                    Error = step.Error
                })
                .ToList(),
            ToolCalls = workflow.ToolResults
                .Select(call => new BedWorkflowToolCall
                {
                    Tool = call.Tool,
                    Input = call.Input,
                    Output = call.Output,
                    StartedAt = call.StartedAt,
                    DurationMs = call.DurationMs,
                    Ok = call.Ok,
                    Error = call.Error
                })
                .ToList(),
            Errors = [.. workflow.Errors],
            AttemptCount = workflow.AttemptCount,
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt,
            Patient = ToPatient(suggestion),
            Best = beds.FirstOrDefault(),
            Alternatives = beds.Skip(1).ToList(),
            Blocker = ToBlocker(suggestion),
            Validation = workflow.Status == AgentWorkflowStatus.Running
                ? null
                : new BedSuggestionValidation
                {
                    Passed = suggestion.ValidationPassed,
                    FailedRules = workflow.ValidationResults
                        .Where(result => !result.Passed)
                        .Select(result => result.Rule)
                        .Distinct()
                        .ToList()
                },
            RequiresApprovalBy = ToApprover(workflow.RequiredApproverRole)
        };
    }

    /// <summary>
    /// A visit that is not waiting for a bed cannot receive a suggestion - with one deliberate
    /// exception. An outpatient is born <c>admitted</c> and never waits for anything, and refusing
    /// it here would answer "this visit is at admitted" when the useful answer is "they do not
    /// need a bed". So it is let through, and the agent says so in its own words.
    /// </summary>
    private async Task EnsureCanReceiveSuggestionAsync(Guid admissionId, CancellationToken ct)
    {
        var admission = await _db.Admissions
            .AsNoTracking()
            .Where(candidate => candidate.Id == admissionId)
            .Select(candidate => new { candidate.Status, candidate.Category })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Admission", admissionId);

        if (!BedPlacementRules.RequiresBed(admission.Category)
            || admission.Status == AdmissionStatus.AwaitingBed)
        {
            return;
        }

        throw new ConflictException(
            MessageCode.AdmissionNotAwaitingBed, EnumWire.ToWire(admission.Status));
    }

    private async Task<List<SuggestedBed>> ToSuggestedBedsAsync(
        BedSuggestionEntity suggestion, CancellationToken ct)
    {
        var candidates = suggestion.Candidates.OrderBy(candidate => candidate.Rank).ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        // Ward name and bed number are read now rather than copied at suggestion time. A label that
        // was right an hour ago and is wrong on the screen is worse than one join.
        var beds = await _beds.ListBedsByIdAsync(
            candidates.Select(candidate => candidate.BedId).ToList(), ct);

        var bedNumbers = beds.ToDictionary(bed => bed.Id, bed => bed.BedNumber);

        var wardNames = await _db.Wards
            .AsNoTracking()
            .Where(ward => candidates.Select(candidate => candidate.WardId).Contains(ward.Id))
            .ToDictionaryAsync(ward => ward.Id, ward => ward.Name, ct);

        return candidates
            // A bed the register has since retired, or a ward that has since closed, is dropped
            // rather than rendered with a blank label: pressing the button on it would fail at
            // assign-bed anyway, and an unnamed row is a button a nurse cannot reason about.
            .Where(candidate => bedNumbers.ContainsKey(candidate.BedId)
                && wardNames.ContainsKey(candidate.WardId))
            .Select(candidate => new SuggestedBed
            {
                BedId = candidate.BedId,
                WardName = wardNames[candidate.WardId],
                BedNumber = bedNumbers[candidate.BedId],
                IsDowngrade = candidate.IsDowngrade,
                RequiresDutyManager = candidate.RequiresDutyManager,
                RulesSatisfied = [.. candidate.RulesSatisfied],
                Rationale = candidate.Rationale
            })
            .ToList();
    }

    private static BedSuggestionPatient? ToPatient(BedSuggestionEntity suggestion)
    {
        if (suggestion.Patient is not { } patient)
        {
            return null;
        }

        return new BedSuggestionPatient
        {
            PatientId = patient.Id,
            PatientCode = patient.PatientCode,
            FullName = patient.FullName,
            Age = PatientAge.InYears(patient.DateOfBirth),
            Gender = patient.Gender,
            AdmissionId = suggestion.AdmissionId,
            AdmissionCategory = suggestion.Admission?.Category,
            Urgency = suggestion.Admission?.Urgency,
            IsInfectious = suggestion.Admission?.IsInfectious,
            Status = suggestion.Admission?.Status
        };
    }

    private static BedSuggestionBlocker? ToBlocker(BedSuggestionEntity suggestion)
        => suggestion is { BlockerCode: { } code, BlockerMessage: { } message }
            ? new BedSuggestionBlocker { Code = code, Message = message }
            : null;

    private static BedApproverRole? ToApprover(StaffRole? role) => role switch
    {
        StaffRole.DutyManager => BedApproverRole.DutyManager,
        StaffRole.WardNurse => BedApproverRole.WardNurse,
        _ => null
    };
}
