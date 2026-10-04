using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using EmergencyCallEntity = CareLanka.Api.Data.Entities.Emergency.EmergencyCall;

namespace CareLanka.Api.Services.Emergency;

public interface IEmergencyAlerts
{
    /// Tells the crew of an ended run to stop, except anyone already moved onto <paramref name="takingOver"/>.
    Task CrewStoodDownAsync(Dispatch dispatch, Dispatch? takingOver = null, CancellationToken cancellationToken = default);

    Task CallerAsync(
        EmergencyCallEntity call,
        NotificationType type,
        string subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default,
        params object[] args);
}
