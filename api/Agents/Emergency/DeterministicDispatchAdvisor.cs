namespace CareLanka.Api.Agents.Emergency;

public sealed class DeterministicDispatchAdvisor : IDispatchAdvisor
{
    public Task<DispatchAdvice> ChooseAsync(DispatchChoiceContext context, CancellationToken ct = default)
    {
        var best = context.Shortlist[0];

        return Task.FromResult(new DispatchAdvice(
            best.Id,
            best.RouteMinutes is { } minutes
                ? $"{best.RegistrationNumber} has the shortest available road estimate among eligible ambulances, about {minutes} minute(s)."
                : $"{best.RegistrationNumber} is eligible, but road estimates are unavailable. Compare locations before confirming."));
    }

    public Task<DispatchAdvice> ExplainDiversionAsync(DiversionContext context, CancellationToken ct = default)
        => Task.FromResult(new DispatchAdvice(
            context.Source.AmbulanceId,
            $"Every ambulance is committed. {context.Source.AmbulanceRegistration} is pre-pickup on a "
            + "lower-priority call and can be turned around; that call returns to the queue."));
}
