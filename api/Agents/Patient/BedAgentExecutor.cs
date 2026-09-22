using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Runs one queued bed-agent workflow to completion and writes the result onto its row.
/// </summary>
/// <remarks>
/// A run that throws must still end as a row marked failed with the reason on it. A row left at
/// pending is a nurse polling for an answer that is never coming, which is worse than being told
/// to assign a bed by hand.
/// </remarks>
public sealed class BedAgentExecutor
{
    private readonly CareLankaDbContext _db;
    private readonly IBedAgent _agent;
    private readonly IBedWorkflowRecorder _recorder;
    private readonly ILogger<BedAgentExecutor> _log;

    public BedAgentExecutor(
        CareLankaDbContext db,
        IBedAgent agent,
        IBedWorkflowRecorder recorder,
        ILogger<BedAgentExecutor> log)
    {
        _db = db;
        _agent = agent;
        _recorder = recorder;
        _log = log;
    }

    public async Task ExecuteAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .FirstOrDefaultAsync(
                row => row.Id == workflowId && row.AgentType == AgentType.PatientAdmissionBed, ct);

        if (workflow is null)
        {
            _log.LogWarning("Bed agent workflow {WorkflowId} is gone; nothing to run.", workflowId);

            return;
        }

        if (workflow.Status != AgentWorkflowStatus.Pending)
        {
            // Already run. Re-running would overwrite an answer a human may already be acting on.
            return;
        }

        try
        {
            var run = await _agent.RunAsync(
                new BedAgentRequest(workflow.EntityId, null),
                onProgress: async (steps, progressCt) =>
                {
                    // The one mid-run checkpoint: everything fast is done, the model call is
                    // about to start. Saved now so a nurse's poll sees it before the run finishes,
                    // rather than nothing until it does.
                    workflow.CompletedSteps = BedWorkflowJson.Write(steps);
                    await _db.SaveChangesAsync(progressCt);
                },
                ct);

            _recorder.Record(workflow, run, workflow.EntityId);
        }
        catch (Exception failure)
        {
            _log.LogError(failure, "Bed agent workflow {WorkflowId} failed.", workflowId);

            _recorder.RecordFailure(workflow, failure);
        }

        await _db.SaveChangesAsync(ct);
    }
}
