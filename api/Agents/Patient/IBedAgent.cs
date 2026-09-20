namespace CareLanka.Api.Agents.Patient;

public interface IBedAgent
{
    Task<BedAgentRun> RunAsync(BedAgentRequest request, CancellationToken ct = default);
}
