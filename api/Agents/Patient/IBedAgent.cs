using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

public interface IBedAgent
{
    /// <summary>
    /// <paramref name="onProgress"/> fires once, right before the one step that can take real
    /// time - the model call in <c>weigh_patient_notes</c> - with every step completed up to that
    /// point. Everything before it runs in milliseconds, so one checkpoint is enough for a caller
    /// to persist "here is what the agent has worked out so far" for a nurse who is watching a
    /// poll rather than nothing at all until the whole run finishes.
    /// </summary>
    Task<BedAgentRun> RunAsync(
        BedAgentRequest request,
        Func<IReadOnlyList<BedAgentStep>, CancellationToken, Task>? onProgress = null,
        CancellationToken ct = default);
}
