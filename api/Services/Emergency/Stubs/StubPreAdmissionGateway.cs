namespace CareLanka.Api.Services.Emergency.Stubs;

// STUB: standing in for Patient Management's POST /admissions/pre-admit. See STUBS.md.
public sealed class StubPreAdmissionGateway(ILogger<StubPreAdmissionGateway> logger) : IPreAdmissionGateway
{
    public Task<PreAdmissionOutcome> SendAsync(PreAdmissionRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Pre-admission for dispatch {DispatchId} was not sent: Patient Management has not built the endpoint yet.",
            request.DispatchId);
        return Task.FromResult(PreAdmissionOutcome.Created);
    }
}
