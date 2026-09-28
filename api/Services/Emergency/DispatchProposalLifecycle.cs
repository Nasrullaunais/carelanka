using CareLanka.Api.Agents.Emergency;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Emergency;

public sealed class DispatchProposalLifecycle : IDispatchProposalLifecycle
{
    public const string Objective = "recommend_best_eligible_ambulance";

    private static readonly DispatchProposalStatus[] OpenStatuses =
    [
        DispatchProposalStatus.Pending,
        DispatchProposalStatus.PendingConfirmation,
        DispatchProposalStatus.PendingApproval
    ];

    private readonly CareLankaDbContext _db;
    private readonly IDispatchRunQueue _queue;
    private readonly TimeProvider _clock;

    public DispatchProposalLifecycle(CareLankaDbContext db, IDispatchRunQueue queue, TimeProvider clock)
    {
        _db = db;
        _queue = queue;
        _clock = clock;
    }

    public DispatchProposal Open(
        Guid callId, CallPriority priority, bool allowDiversion, IReadOnlyCollection<Guid> excludeAmbulanceIds)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.DispatchRouting,
            EntityType = "EmergencyCall",
            EntityId = callId,
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
            EmergencyCallId = callId,
            CallPriority = priority,
            Status = DispatchProposalStatus.Pending,
            AllowDiversion = allowDiversion,
            ExcludeAmbulanceIdsJson = excludeAmbulanceIds.Count > 0
                ? DispatchWorkflowJson.Write(excludeAmbulanceIds.Distinct().ToList()) : null
        };
        _db.DispatchProposals.Add(proposal);
        return proposal;
    }

    public async Task<DispatchProposal?> WithdrawOpenAsync(
        Guid callId, DispatchWithdrawalReason reason, CancellationToken cancellationToken = default)
    {
        var proposal = await _db.DispatchProposals.SingleOrDefaultAsync(
            row => row.EmergencyCallId == callId && OpenStatuses.Contains(row.Status), cancellationToken);
        if (proposal is null) return null;

        var now = _clock.GetUtcNow();
        proposal.Status = DispatchProposalStatus.Withdrawn;
        proposal.WithdrawalReason = reason;
        proposal.WithdrawnAt = now;

        var workflow = await _db.AgentWorkflows.SingleOrDefaultAsync(row => row.Id == proposal.WorkflowId, cancellationToken);
        if (workflow is not null)
        {
            workflow.Status = AgentWorkflowStatus.Withdrawn;
            workflow.CompletedAt = now;
        }

        return proposal;
    }

    public async Task<IReadOnlyList<Guid>> CarriedExclusionsAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        var json = await _db.DispatchProposals.AsNoTracking()
            .Where(row => row.EmergencyCallId == callId)
            .OrderByDescending(row => row.CreatedAt)
            .Select(row => row.ExcludeAmbulanceIdsJson)
            .FirstOrDefaultAsync(cancellationToken);
        return DispatchWorkflowJson.Read<List<Guid>>(json) ?? [];
    }

    public void Wake(DispatchProposal proposal) => _queue.Enqueue(proposal.Id);
}
