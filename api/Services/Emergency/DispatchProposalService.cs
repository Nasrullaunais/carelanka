using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Emergency;

/// <summary>
/// The dispatch agent's entry point and its record. It guards the call, persists the workflow row
/// before anything executes, hands planning to the background worker, and applies a human decision
/// through the same <see cref="IDispatchService"/> the manual dispatch endpoint uses.
/// </summary>
public sealed class DispatchProposalService : IDispatchProposalService
{
    public const string Objective = "recommend_best_eligible_ambulance";

    private readonly CareLankaDbContext _db;
    private readonly IDispatchService _dispatches;
    private readonly IDispatchRunQueue _queue;
    private readonly IAmbulanceEligibilityService _eligibility;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public DispatchProposalService(
        CareLankaDbContext db, IDispatchService dispatches, IDispatchRunQueue queue,
        IAmbulanceEligibilityService eligibility, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _dispatches = dispatches;
        _queue = queue;
        _eligibility = eligibility;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DispatchProposalSummary> StartAsync(CreateDispatchProposalRequest request, CancellationToken ct = default)
    {
        var call = await _db.EmergencyCalls
            .Include(x => x.Dispatches)
            .SingleOrDefaultAsync(x => x.Id == request.EmergencyCallId, ct)
            ?? throw new NotFoundException("Emergency call", request.EmergencyCallId);

        var hasOpenProposal = await _db.DispatchProposals.AnyAsync(
            x => x.EmergencyCallId == request.EmergencyCallId &&
                (x.Status == DispatchProposalStatus.Pending
                    || x.Status == DispatchProposalStatus.PendingConfirmation
                    || x.Status == DispatchProposalStatus.PendingApproval), ct);

        if (hasOpenProposal || call.Status != CallStatus.Received || call.Dispatches.Any(x => x.Status.IsLive()))
        {
            throw new ConflictException(MessageCode.DispatchProposalConflict);
        }

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.DispatchRouting,
            EntityType = "EmergencyCall",
            EntityId = call.Id,
            CorrelationId = Guid.NewGuid(),
            Objective = Objective,
            Status = AgentWorkflowStatus.Pending,
            StartedAt = _clock.GetUtcNow(),
            AttemptCount = 1
        };
        _db.AgentWorkflows.Add(workflow);

        var proposal = new DispatchProposal
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            EmergencyCallId = call.Id,
            CallPriority = call.Priority,
            Status = DispatchProposalStatus.Pending,
            AllowDiversion = request.AllowDiversion,
            ExcludeAmbulanceIdsJson = request.ExcludeAmbulanceIds is { Count: > 0 }
                ? DispatchWorkflowJson.Write(request.ExcludeAmbulanceIds) : null
        };
        _db.DispatchProposals.Add(proposal);

        await _db.SaveChangesAsync(ct);
        _queue.Enqueue(proposal.Id);

        return await ToSummaryAsync(proposal, ct);
    }

    public async Task<PagedResult<DispatchProposalSummary>> ListAsync(ListDispatchProposalsRequest request, CancellationToken ct = default)
    {
        var query = _db.DispatchProposals.AsNoTracking();

        if (request.Status is { } status) query = query.Where(x => x.Status == status);
        if (request.EmergencyCallId is { } callId) query = query.Where(x => x.EmergencyCallId == callId);
        if (request.IsDiversion is { } isDiversion) query = query.Where(x => x.IsDiversion == isDiversion);

        var totalItems = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(ct);

        var registrations = await RegistrationsAsync(rows.Select(x => x.ProposedAmbulanceId), ct);

        return PagedResult<DispatchProposalSummary>.From(
            rows.Select(row => ToSummary(row, registrations)).ToList(), request.Page, request.PageSize, totalItems);
    }

    public async Task<DispatchProposalDetail> GetAsync(Guid proposalId, CancellationToken ct = default)
        => await ToDetailAsync(await LoadAsync(proposalId, ct), ct);

    public async Task<DispatchProposalDetail> ConfirmAsync(Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await LoadAsync(proposalId, ct);

        if (proposal.Status != DispatchProposalStatus.PendingConfirmation || proposal.ProposedAmbulanceId is not { } ambulanceId)
        {
            throw new ConflictException(MessageCode.DispatchProposalNotConfirmable, "not pending confirmation");
        }

        var eligible = await IsEligibleAsync(ambulanceId, ct);
        var check = DispatchProposalValidator.AmbulanceEligible(eligible, _clock.GetUtcNow());
        await AppendValidationAsync(proposal, check, ct);

        if (!eligible)
        {
            await _db.SaveChangesAsync(ct);
            throw new DispatchProposalRejectedException(
                MessageCode.DispatchProposalNotConfirmable, null, [check.Check], check.Detail);
        }

        var dispatch = await _dispatches.DispatchFromProposalAsync(proposal.EmergencyCallId, ambulanceId, proposal.Id, ct);

        proposal.Status = DispatchProposalStatus.Executed;
        proposal.ResultingDispatchId = dispatch.Id;
        proposal.ReviewedByStaffMemberId = _currentUser.Id;
        proposal.ReviewedAt = _clock.GetUtcNow();
        await SetWorkflowExecutedAsync(proposal, ct);
        await _db.SaveChangesAsync(ct);

        return await ToDetailAsync(proposal, ct);
    }

    public async Task<DispatchProposalDetail> ApproveAsync(Guid proposalId, ApproveDispatchProposalRequest request, CancellationToken ct = default)
    {
        var proposal = await LoadAsync(proposalId, ct);

        if (proposal.Status != DispatchProposalStatus.PendingApproval
            || proposal.ProposedAmbulanceId is not { } ambulanceId
            || proposal.SourceDispatchId is not { } sourceDispatchId)
        {
            throw new ConflictException(MessageCode.DispatchProposalNotApprovable, "not pending approval");
        }

        var source = await _db.Dispatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sourceDispatchId, ct);
        var sourceStillPrePickup = source is not null && source.Status.IsPrePickup();
        var prePickupCheck = DispatchProposalValidator.SourcePrePickup(source?.Status ?? DispatchStatus.HandedOver, _clock.GetUtcNow());
        await AppendValidationAsync(proposal, prePickupCheck, ct);

        var eligible = await IsEligibleAsync(ambulanceId, ct);
        var eligibleCheck = DispatchProposalValidator.AmbulanceEligible(eligible, _clock.GetUtcNow());
        await AppendValidationAsync(proposal, eligibleCheck, ct);

        if (!sourceStillPrePickup || !eligible)
        {
            await _db.SaveChangesAsync(ct);
            var failedChecks = new List<string>();
            if (!sourceStillPrePickup) failedChecks.Add(prePickupCheck.Check);
            if (!eligible) failedChecks.Add(eligibleCheck.Check);

            throw new DispatchProposalRejectedException(
                MessageCode.DispatchProposalNotApprovable,
                sourceStillPrePickup ? DiversionBlockReason.AmbulanceNotAvailable : DiversionBlockReason.PatientAlreadyReached,
                failedChecks,
                string.Join(", ", failedChecks));
        }

        var dispatch = await _dispatches.ApplyDiversionAsync(
            sourceDispatchId, proposal.EmergencyCallId, ambulanceId, proposal.Id, request.Notes, ct);

        proposal.Status = DispatchProposalStatus.Executed;
        proposal.ResultingDispatchId = dispatch.Id;
        proposal.ReviewedByStaffMemberId = _currentUser.Id;
        proposal.ReviewedAt = _clock.GetUtcNow();
        proposal.ReviewNotes = request.Notes?.Trim();
        await SetWorkflowExecutedAsync(proposal, ct);
        await _db.SaveChangesAsync(ct);

        return await ToDetailAsync(proposal, ct);
    }

    public async Task<DispatchProposalDetail> RejectAsync(Guid proposalId, RejectDispatchProposalRequest request, CancellationToken ct = default)
    {
        var proposal = await LoadAsync(proposalId, ct);

        if (proposal.Status != DispatchProposalStatus.PendingConfirmation && proposal.Status != DispatchProposalStatus.PendingApproval)
        {
            throw new IllegalTransitionException("DispatchProposal", proposal.Status.ToString(), "rejected");
        }

        proposal.Status = DispatchProposalStatus.Rejected;
        proposal.RejectionReason = request.Reason;
        proposal.ReviewNotes = request.Notes?.Trim();
        proposal.ReviewedByStaffMemberId = _currentUser.Id;
        proposal.ReviewedAt = _clock.GetUtcNow();

        var workflow = await _db.AgentWorkflows.SingleOrDefaultAsync(x => x.Id == proposal.WorkflowId, ct);
        if (workflow is not null)
        {
            workflow.Status = AgentWorkflowStatus.Rejected;
            workflow.ReviewedByStaffMemberId = _currentUser.Id;
            workflow.ReviewedAt = proposal.ReviewedAt;
            workflow.ReviewNotes = proposal.ReviewNotes;
        }

        await _db.SaveChangesAsync(ct);

        return await ToDetailAsync(proposal, ct);
    }

    private async Task SetWorkflowExecutedAsync(DispatchProposal proposal, CancellationToken ct)
    {
        var workflow = await _db.AgentWorkflows.SingleOrDefaultAsync(x => x.Id == proposal.WorkflowId, ct);
        if (workflow is null) return;
        workflow.Status = AgentWorkflowStatus.Executed;
        workflow.ReviewedByStaffMemberId = _currentUser.Id;
        workflow.ReviewedAt = proposal.ReviewedAt;
    }

    private async Task AppendValidationAsync(DispatchProposal proposal, DispatchValidationResult check, CancellationToken ct)
    {
        var workflow = await _db.AgentWorkflows.SingleOrDefaultAsync(x => x.Id == proposal.WorkflowId, ct);
        if (workflow is null) return;

        var checks = DispatchWorkflowJson.Read<List<DispatchValidationResult>>(workflow.ValidationResults) ?? [];
        checks.Add(check);
        workflow.ValidationResults = DispatchWorkflowJson.Write(checks);
    }

    private async Task<bool> IsEligibleAsync(Guid ambulanceId, CancellationToken ct)
    {
        var ambulance = await _db.Ambulances.Include(x => x.CrewAssignments)
            .SingleOrDefaultAsync(x => x.Id == ambulanceId, ct);
        if (ambulance is null) return false;

        var activeDispatchId = await _db.Dispatches
            .Where(x => x.AmbulanceId == ambulanceId && DispatchStatusExtensions.LiveStatuses.Contains(x.Status))
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var crewCount = ambulance.CrewAssignments.Count(x => x.UnassignedAt == null);

        var eligibility = _eligibility.Decide(new AmbulanceEligibilityFacts(
            ambulance.IsActive, ambulance.Status, crewCount, activeDispatchId,
            ambulance.CurrentLatitude.HasValue && ambulance.CurrentLongitude.HasValue, ambulance.LocationUpdatedAt));

        return eligibility.IsEligible;
    }


    private async Task<DispatchProposal> LoadAsync(Guid proposalId, CancellationToken ct)
        => await _db.DispatchProposals.SingleOrDefaultAsync(x => x.Id == proposalId, ct)
            ?? throw new NotFoundException("DispatchProposal", proposalId);

    private async Task<DispatchProposalSummary> ToSummaryAsync(DispatchProposal proposal, CancellationToken ct)
    {
        var registrations = await RegistrationsAsync([proposal.ProposedAmbulanceId], ct);
        return ToSummary(proposal, registrations);
    }

    private static DispatchProposalSummary ToSummary(DispatchProposal proposal, IReadOnlyDictionary<Guid, string> registrations)
        => new()
        {
            Id = proposal.Id,
            WorkflowId = proposal.WorkflowId,
            EmergencyCallId = proposal.EmergencyCallId,
            CallPriority = proposal.CallPriority,
            Status = proposal.Status,
            Outcome = proposal.Outcome,
            IsDiversion = proposal.IsDiversion,
            ProposedAmbulanceRegistration = proposal.ProposedAmbulanceId is { } id && registrations.TryGetValue(id, out var reg) ? reg : null,
            EstimatedMinutesToScene = proposal.EstimatedMinutesToScene,
            CreatedAt = proposal.CreatedAt
        };

    private async Task<DispatchProposalDetail> ToDetailAsync(DispatchProposal proposal, CancellationToken ct)
    {
        var registrations = await RegistrationsAsync([proposal.ProposedAmbulanceId, proposal.ReplacementAmbulanceId], ct);
        var workflow = await _db.AgentWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proposal.WorkflowId, ct);

        var validation = (workflow is null ? null : DispatchWorkflowJson.Read<List<DispatchValidationResult>>(workflow.ValidationResults)) ?? [];
        var proposedAmbulance = proposal.ProposedAmbulanceId is { } proposedId
            ? await _db.Ambulances.AsNoTracking()
                .Where(ambulance => ambulance.Id == proposedId)
                .Select(ambulance => new
                {
                    ambulance.IsActive,
                    ambulance.Status,
                    ambulance.CurrentLatitude,
                    ambulance.CurrentLongitude,
                    ambulance.LocationUpdatedAt,
                    CrewCount = ambulance.CrewAssignments.Count(assignment => assignment.UnassignedAt == null),
                    HasActiveDispatch = ambulance.Dispatches.Any(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status))
                })
                .SingleOrDefaultAsync(ct)
            : null;
        var proposedEligibility = proposedAmbulance is null
            ? null
            : _eligibility.Decide(new AmbulanceEligibilityFacts(
                proposedAmbulance.IsActive,
                proposedAmbulance.Status,
                proposedAmbulance.CrewCount,
                proposedAmbulance.HasActiveDispatch ? proposal.SourceDispatchId ?? Guid.Empty : null,
                proposedAmbulance.CurrentLatitude.HasValue && proposedAmbulance.CurrentLongitude.HasValue,
                proposedAmbulance.LocationUpdatedAt));

        return new DispatchProposalDetail
        {
            Id = proposal.Id,
            WorkflowId = proposal.WorkflowId,
            EmergencyCallId = proposal.EmergencyCallId,
            CallPriority = proposal.CallPriority,
            Status = proposal.Status,
            Outcome = proposal.Outcome,
            IsDiversion = proposal.IsDiversion,
            ProposedAmbulanceRegistration = proposal.ProposedAmbulanceId is { } id && registrations.TryGetValue(id, out var reg) ? reg : null,
            EstimatedMinutesToScene = proposal.EstimatedMinutesToScene,
            CreatedAt = proposal.CreatedAt,
            Objective = Objective,
            ProposedAmbulanceId = proposal.ProposedAmbulanceId,
            ProposedAmbulanceCurrentCrewCount = proposedAmbulance?.CrewCount,
            ProposedAmbulanceRequiredCrewCount = proposedEligibility?.RequiredCrewCount,
            Rationale = proposal.Rationale,
            DiversionImpact = proposal.IsDiversion && proposal.SourceDispatchId is { } sourceId
                ? new DiversionImpact
                {
                    SourceDispatchId = sourceId,
                    SourceCallId = proposal.SourceCallId ?? Guid.Empty,
                    SourceCallPriority = proposal.SourceCallPriority ?? CallPriority.Low,
                    SourceCallAddressLabel = proposal.SourceCallAddressLabel,
                    SourceDispatchStatus = proposal.SourceDispatchStatus ?? DispatchStatus.Assigned,
                    SourceCallWaitingMinutesSoFar = proposal.SourceCallWaitingMinutesSoFar ?? 0,
                    SourceCallAdditionalWaitMinutes = proposal.SourceCallAdditionalWaitMinutes ?? 0,
                    ReplacementAmbulanceId = proposal.ReplacementAmbulanceId,
                    ReplacementAmbulanceRegistration = proposal.ReplacementAmbulanceId is { } r && registrations.TryGetValue(r, out var rReg) ? rReg : null,
                    MinutesSavedForThisCall = proposal.MinutesSavedForThisCall ?? 0
                }
                : null,
            Plan = (workflow is null ? null : DispatchWorkflowJson.Read<List<DispatchPlanStep>>(workflow.Plan)) ?? [],
            Validation = validation,
            ToolCalls = (workflow is null ? null : DispatchWorkflowJson.Read<List<DispatchToolCall>>(workflow.ToolResults)) ?? [],
            Errors = (workflow is null ? null : DispatchWorkflowJson.Read<List<string>>(workflow.Errors))
                ?.Select(message => new DispatchProposalError { Step = "run", Message = message, OccurredAt = proposal.CreatedAt }).ToList() ?? [],
            AttemptCount = workflow?.AttemptCount ?? 0,
            StartedAt = workflow?.StartedAt,
            CompletedAt = workflow?.CompletedAt,
            ResultingDispatchId = proposal.ResultingDispatchId,
            ReviewedByStaffMemberId = proposal.ReviewedByStaffMemberId,
            ReviewedAt = proposal.ReviewedAt,
            ReviewNotes = proposal.ReviewNotes,
            RejectionReason = proposal.RejectionReason
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string>> RegistrationsAsync(IEnumerable<Guid?> ambulanceIds, CancellationToken ct)
    {
        var ids = ambulanceIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _db.Ambulances.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.RegistrationNumber, ct);
    }
}
