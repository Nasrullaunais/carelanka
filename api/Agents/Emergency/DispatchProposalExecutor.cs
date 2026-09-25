using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Emergency;

/// <summary>
/// Runs one queued dispatch proposal to completion and writes the result onto its row and the
/// shared workflow record. A run that throws still ends as a row marked failed with the reason on
/// it - a row left at pending is a dispatcher polling for an answer that never comes.
/// </summary>
public sealed class DispatchProposalExecutor
{
    private readonly CareLankaDbContext _db;
    private readonly IDispatchAgent _agent;
    private readonly ILogger<DispatchProposalExecutor> _log;

    public DispatchProposalExecutor(CareLankaDbContext db, IDispatchAgent agent, ILogger<DispatchProposalExecutor> log)
    {
        _db = db;
        _agent = agent;
        _log = log;
    }

    public async Task ExecuteAsync(Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await _db.DispatchProposals
            .Include(row => row.EmergencyCall)
            .FirstOrDefaultAsync(row => row.Id == proposalId, ct);

        if (proposal is null || proposal.Status != DispatchProposalStatus.Pending)
        {
            return;
        }

        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(row => row.Id == proposal.WorkflowId, ct);

        try
        {
            var excludeAmbulanceIds = DispatchWorkflowJson.Read<List<Guid>>(proposal.ExcludeAmbulanceIdsJson) ?? [];

            var run = await _agent.RunAsync(
                new DispatchAgentRequest(
                    proposal.EmergencyCallId, proposal.CallPriority,
                    proposal.EmergencyCall.Latitude, proposal.EmergencyCall.Longitude,
                    proposal.AllowDiversion, excludeAmbulanceIds),
                ct);

            proposal.Outcome = run.Outcome;
            proposal.IsDiversion = run.IsDiversion;
            proposal.ProposedAmbulanceId = run.ProposedAmbulanceId;
            proposal.EstimatedMinutesToScene = run.EstimatedMinutesToScene;
            proposal.Rationale = run.Rationale;

            if (run.DiversionImpact is { } impact)
            {
                proposal.SourceDispatchId = impact.SourceDispatchId;
                proposal.SourceCallId = impact.SourceCallId;
                proposal.SourceCallPriority = impact.SourceCallPriority;
                proposal.SourceCallAddressLabel = impact.SourceCallAddressLabel;
                proposal.SourceDispatchStatus = impact.SourceDispatchStatus;
                proposal.SourceCallWaitingMinutesSoFar = impact.SourceCallWaitingMinutesSoFar;
                proposal.SourceCallAdditionalWaitMinutes = impact.SourceCallAdditionalWaitMinutes;
                proposal.ReplacementAmbulanceId = impact.ReplacementAmbulanceId;
                proposal.MinutesSavedForThisCall = impact.MinutesSavedForThisCall;
            }

            proposal.Status = run.Outcome switch
            {
                DispatchOutcome.FreeAmbulanceProposed => DispatchProposalStatus.PendingConfirmation,
                DispatchOutcome.DiversionProposed => DispatchProposalStatus.PendingApproval,
                DispatchOutcome.NoAmbulanceAvailable => DispatchProposalStatus.Failed,
                _ => DispatchProposalStatus.Failed
            };

            if (workflow is not null)
            {
                workflow.Plan = DispatchWorkflowJson.Write(run.Plan);
                workflow.CompletedSteps = DispatchWorkflowJson.Write(run.Plan);
                workflow.ToolResults = DispatchWorkflowJson.Write(run.ToolCalls);
                workflow.ValidationResults = DispatchWorkflowJson.Write(run.Validation);
                workflow.Errors = run.Errors.Count == 0 ? null : DispatchWorkflowJson.Write(run.Errors);
                workflow.FinalOutcome = CareLanka.Api.Common.Persistence.EnumWire.ToWire(run.Outcome);
                workflow.CompletedAt = DateTimeOffset.UtcNow;
                workflow.RequiredApproverRole = StaffRole.DutyManager;
                workflow.Status = proposal.Status switch
                {
                    DispatchProposalStatus.PendingConfirmation => AgentWorkflowStatus.Pending,
                    DispatchProposalStatus.PendingApproval => AgentWorkflowStatus.PendingApproval,
                    _ => AgentWorkflowStatus.Failed
                };
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            _log.LogError(failure, "Dispatch proposal {ProposalId} failed.", proposalId);
            proposal.Status = DispatchProposalStatus.Failed;
            proposal.Outcome = DispatchOutcome.Failed;

            if (workflow is not null)
            {
                workflow.Status = AgentWorkflowStatus.Failed;
                workflow.Errors = DispatchWorkflowJson.Write(new[] { failure.Message });
                workflow.CompletedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
