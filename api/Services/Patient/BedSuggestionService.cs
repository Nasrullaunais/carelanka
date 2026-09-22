using CareLanka.Api.Agents;
using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The bed agent's entry point and its record. It guards the run, persists the plan before
/// anything executes, hands the work to the background worker, and answers the poll.
/// <para>
/// Nothing here touches an admission, a bed or an assignment. The pause the assignment asks for is
/// a workflow row sitting at pending_approval, not a half-finished write in the domain: the
/// patient stays at awaiting_bed and the bed stays free for anybody, right up until a human
/// presses a button on the manual endpoint.
/// </para>
/// </summary>
public sealed class BedSuggestionService : IBedSuggestionService
{
    public const string Objective = "suggest_bed";

    private readonly CareLankaDbContext _db;
    private readonly IBedAgent _agent;
    private readonly IBedAgentTools _tools;
    private readonly IBedWorkflowRecorder _recorder;
    private readonly IAgentRunQueue _queue;

    public BedSuggestionService(
        CareLankaDbContext db,
        IBedAgent agent,
        IBedAgentTools tools,
        IBedWorkflowRecorder recorder,
        IAgentRunQueue queue)
    {
        _db = db;
        _agent = agent;
        _tools = tools;
        _recorder = recorder;
        _queue = queue;
    }

    public async Task<BedWorkflowAccepted> StartAsync(
        BedSuggestionRequest request, CancellationToken ct = default)
    {
        var identifier = request.PatientIdentifier?.Trim();
        var admissionId = await GuardAsync(request.AdmissionId, identifier, ct);

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.PatientAdmissionBed,
            EntityType = BedWorkflowRecorder.WorkflowEntityType,
            EntityId = admissionId ?? Guid.Empty,
            CorrelationId = Guid.NewGuid(),
            Objective = Objective,
            Plan = BedWorkflowJson.Write(BedAgent.Plan),
            Status = AgentWorkflowStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        };

        _db.AgentWorkflows.Add(workflow);

        // The plan is on disk before a single tool runs. A run that dies mid-flight leaves a row
        // saying what it set out to do, rather than no trace at all.
        await _db.SaveChangesAsync(ct);

        if (admissionId is null)
        {
            // Nobody to suggest a bed for. That answer is one read and no model call, so it is
            // settled here rather than queued - there is no work for a worker to do.
            _recorder.Record(
                workflow,
                await _agent.RunAsync(new BedAgentRequest(null, identifier), ct: ct),
                null);

            await _db.SaveChangesAsync(ct);
        }
        else
        {
            _queue.Enqueue(workflow.Id);
        }

        return new BedWorkflowAccepted
        {
            WorkflowId = workflow.Id,
            AdmissionId = admissionId,
            Status = WireStatus(workflow.Status),
            PollUrl = $"/api/bed-workflows/{workflow.Id}"
        };
    }

    public async Task<BedWorkflowSummary> GetAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .Include(row => row.ProposedChanges)
            .FirstOrDefaultAsync(
                row => row.Id == workflowId && row.AgentType == AgentType.PatientAdmissionBed, ct)
            ?? throw new NotFoundException("AgentWorkflow", workflowId);

        var beds = workflow.ProposedChanges
            .OrderBy(change => change.Sequence)
            .Select(ToSuggestedBed)
            .ToList();

        var validation = BedWorkflowJson.Read<PersistedValidation>(workflow.ValidationResults)
            ?? new PersistedValidation();

        return new BedWorkflowSummary
        {
            WorkflowId = workflow.Id,
            AdmissionId = workflow.EntityId == Guid.Empty ? null : workflow.EntityId,
            Objective = workflow.Objective,
            Status = WireStatus(workflow.Status),
            Outcome = Outcome(workflow.FinalOutcome),
            Plan = BedWorkflowJson.Read<List<string>>(workflow.Plan) ?? [],
            Steps = BedWorkflowJson.Read<List<BedAgentStep>>(workflow.CompletedSteps) ?? [],
            Patient = await PatientAsync(workflow.EntityId, ct),
            Best = beds.FirstOrDefault(),
            Alternatives = beds.Skip(1).ToList(),
            Blocker = validation.Blocker,
            Validation = new BedWorkflowValidation
            {
                Passed = validation.Passed,
                FailedRules = validation.FailedRules
            },
            RequiresApprovalBy = Approver(workflow.RequiredApproverRole),
            Retries = Math.Max(0, workflow.AttemptCount - 1)
        };
    }

    /// <summary>
    /// The two answers that are errors rather than outcomes: an admission nobody has heard of, and
    /// a visit that already has a bed or has left. Everything else the agent can say for itself.
    /// </summary>
    private async Task<Guid?> GuardAsync(
        Guid? admissionId, string? identifier, CancellationToken ct)
    {
        if (admissionId is { } id)
        {
            var admission = await _db.Admissions
                .AsNoTracking()
                .Include(row => row.Patient)
                .FirstOrDefaultAsync(row => row.Id == id, ct)
                ?? throw new NotFoundException("Admission", id);

            EnsureWaitingForABed(admission);

            return admission.Id;
        }

        var lookup = await _tools.FindPatientAsync(identifier!, ct);

        if (lookup?.OpenAdmission is not { } open)
        {
            return null;
        }

        open.Patient = lookup.Patient;
        EnsureWaitingForABed(open);

        return open.Id;
    }

    private static void EnsureWaitingForABed(AdmissionEntity admission)
    {
        if (admission.Category is null)
        {
            throw new ConflictException(MessageCode.AdmissionNotYetClassified, admission.Id);
        }

        if (!BedPlacementRules.RequiresBed(admission.Category))
        {
            // An outpatient is admitted from the moment their record is opened, so the status check
            // below would refuse them. "They do not need a bed" is an answer the run should give.
            return;
        }

        if (admission.Status != AdmissionStatus.AwaitingBed)
        {
            throw new ConflictException(
                MessageCode.BedSuggestionNotPossible, admission.Patient.FullName);
        }
    }

    private static SuggestedBed ToSuggestedBed(AgentProposedChange change)
    {
        var stored = BedWorkflowJson.Read<PersistedBed>(change.Payload) ?? new PersistedBed();

        return new SuggestedBed
        {
            BedId = change.ProposedBedId ?? Guid.Empty,
            WardName = stored.WardName,
            BedNumber = stored.BedNumber,
            IsDowngrade = stored.IsDowngrade,
            RequiresDutyManager = stored.RequiresDutyManager,
            RulesSatisfied = stored.RulesSatisfied,
            FitFactors = stored.FitFactors,
            Rationale = stored.Rationale
        };
    }

    private async Task<BedSuggestionPatient?> PatientAsync(Guid admissionId, CancellationToken ct)
    {
        if (admissionId == Guid.Empty)
        {
            return null;
        }

        var admission = await _db.Admissions
            .AsNoTracking()
            .Include(row => row.Patient)
            .FirstOrDefaultAsync(row => row.Id == admissionId, ct);

        if (admission is null)
        {
            return null;
        }

        var patient = admission.Patient;

        return new BedSuggestionPatient
        {
            PatientId = patient.Id,
            PatientCode = patient.PatientCode,
            FullName = patient.FullName,
            Age = Age(patient.DateOfBirth),
            Gender = patient.Gender,
            AdmissionId = admission.Id,
            AdmissionCategory = admission.Category,
            Urgency = admission.Urgency,
            IsInfectious = admission.IsInfectious,
            Status = admission.Status
        };
    }

    private static int? Age(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is not { } born)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - born.Year;

        return born > today.AddYears(-age) ? age - 1 : age;
    }

    private static BedWorkflowStatus WireStatus(AgentWorkflowStatus status) => status switch
    {
        AgentWorkflowStatus.Pending => BedWorkflowStatus.Running,
        AgentWorkflowStatus.PendingApproval => BedWorkflowStatus.AwaitingApproval,
        AgentWorkflowStatus.Failed => BedWorkflowStatus.Failed,
        _ => BedWorkflowStatus.Completed
    };

    private static BedApproverRole? Approver(StaffRole? role) => role switch
    {
        StaffRole.WardNurse => BedApproverRole.WardNurse,
        StaffRole.DutyManager => BedApproverRole.DutyManager,
        _ => null
    };

    private static BedAgentOutcome? Outcome(string? finalOutcome)
        => finalOutcome is null ? null : EnumWire.FromWire<BedAgentOutcome>(finalOutcome);
}
