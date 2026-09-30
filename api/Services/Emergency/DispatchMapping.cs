using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.DTOs.Emergency;
using EmergencyCallEntity = CareLanka.Api.Data.Entities.Emergency.EmergencyCall;

namespace CareLanka.Api.Services.Emergency;

internal static class DispatchMapping
{
    public static T ToCallDispatch<T>(Dispatch dispatch, EmergencyCallEntity call, DateTimeOffset now, int acknowledgementTimeoutSeconds)
        where T : CallDispatch, new()
        => new()
        {
            Id = dispatch.Id, EmergencyCallId = dispatch.EmergencyCallId, AmbulanceId = dispatch.AmbulanceId,
            AmbulanceRegistration = dispatch.Ambulance.RegistrationNumber, CallPriority = call.Priority,
            Status = dispatch.Status, DispatchedAt = dispatch.DispatchedAt, CompletedAt = dispatch.CompletedAt,
            AcknowledgedAt = dispatch.AcknowledgedAt, AcknowledgedByStaffId = dispatch.AcknowledgedByStaffId,
            DeclinedReason = dispatch.DeclinedReason, CancellationReason = dispatch.CancellationReason,
            ReassignmentReason = dispatch.ReassignmentReason, SupersededByDispatchId = dispatch.SupersededByDispatchId,
            HandoverNotes = dispatch.HandoverNotes, PatientCondition = dispatch.PatientCondition,
            CrewCount = dispatch.Crew.Count,
            AcknowledgementOverdue = dispatch.IsAcknowledgementOverdue(now, acknowledgementTimeoutSeconds)
        };
}
