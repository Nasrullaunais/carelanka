using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Emergency;

public sealed class PreAdmissionGateway(IAdmissionService admissions) : IPreAdmissionGateway
{
    public async Task<PreAdmissionOutcome> SendAsync(
        PreAdmissionRequest request, CancellationToken cancellationToken = default)
    {
        var body = new PreAdmitRequest
        {
            DispatchId = request.DispatchId.ToString(),
            PatientIsCaller = request.PatientIsCaller,
            CallerUserId = request.CallerUserId,
            PatientId = request.PatientId,
            ExpectedArrival = request.ExpectedArrival,
            Urgency = request.Urgency
        };

        try
        {
            await admissions.PreAdmitAsync(body, cancellationToken);
            return PreAdmissionOutcome.Created;
        }
        catch (ConflictException)
        {
            return PreAdmissionOutcome.AlreadyExists;
        }
        catch (NotFoundException)
        {
            return PreAdmissionOutcome.Rejected;
        }
    }
}
