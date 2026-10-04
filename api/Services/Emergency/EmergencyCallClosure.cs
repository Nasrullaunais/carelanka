using CareLanka.Api.Data.Enums;
using EmergencyCallEntity = CareLanka.Api.Data.Entities.Emergency.EmergencyCall;

namespace CareLanka.Api.Services.Emergency;

public static class EmergencyCallClosure
{
    public static bool IsClosed(this CallStatus status) => status is CallStatus.Completed or CallStatus.Cancelled;

    // Only a call still waiting for an ambulance is "waiting"; once one is sent or the call closes, the clock stops.
    public static int WaitingMinutes(this EmergencyCallEntity call, DateTimeOffset now)
        => WaitingMinutes(call.Status, call.CreatedAt, now);

    public static int WaitingMinutes(CallStatus status, DateTimeOffset createdAt, DateTimeOffset now)
        => status == CallStatus.Received ? Math.Max(0, (int)(now - createdAt).TotalMinutes) : 0;

    public static void Close(
        this EmergencyCallEntity call, CallStatus status, EmergencyCallOutcome outcome, string? notes, DateTimeOffset now)
    {
        call.Status = status;
        call.Outcome = outcome;
        call.OutcomeNotes = notes;
        call.Transported = outcome == EmergencyCallOutcome.Transported;
        call.ClosedAt = now;

        // Nobody reviewed it in time, and there is nothing left to cancel.
        if (call.CancellationRequestStatus == CancellationRequestStatus.Pending)
        {
            call.CancellationRequestStatus = CancellationRequestStatus.Expired;
            call.CancellationReviewedAt = now;
        }
    }
}
