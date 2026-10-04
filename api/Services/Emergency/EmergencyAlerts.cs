using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Patient;
using EmergencyCallEntity = CareLanka.Api.Data.Entities.Emergency.EmergencyCall;

namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyAlerts(INotifier notifier, IPatientService patients) : IEmergencyAlerts
{
    public async Task CrewStoodDownAsync(Dispatch dispatch, Dispatch? takingOver = null, CancellationToken cancellationToken = default)
    {
        // In a diversion the same crew drives on to the new call; they already got its "new run" alert.
        var stillWorking = takingOver?.Crew.Select(crew => crew.StaffMemberId).ToHashSet() ?? [];
        foreach (var crewMember in dispatch.Crew.Where(crew => !stillWorking.Contains(crew.StaffMemberId)))
        {
            await notifier.NotifyAsync(NotificationType.DispatchCancelled, Recipients.Staff(crewMember.StaffMemberId),
                new NotificationSubject("dispatch", dispatch.Id), cancellationToken);
        }
    }

    // Updates go to whoever placed the call from the app, which for "someone else" is not the patient.
    public async Task CallerAsync(
        EmergencyCallEntity call,
        NotificationType type,
        string subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default,
        params object[] args)
    {
        if (call.CallerUserId is not { } callerId)
        {
            return;
        }

        var caller = await patients.FindByUserAccountIdAsync(callerId, cancellationToken);
        if (caller is null)
        {
            return;
        }

        await notifier.NotifyAsync(type, Recipients.Patient(caller.Id),
            new NotificationSubject(subjectType, subjectId), cancellationToken, args);
    }
}
