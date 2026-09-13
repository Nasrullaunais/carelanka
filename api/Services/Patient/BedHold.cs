using System.Linq.Expressions;
using CareLanka.Api.Data.Enums;
using BedAssignmentEntity = CareLanka.Api.Data.Entities.Patient.BedAssignment;

namespace CareLanka.Api.Services.Patient;

public static class BedHold
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

    public static DateTimeOffset ExpiresAt(DateTimeOffset? expectedArrival, DateTimeOffset now)
    {
        var from = expectedArrival is { } arrival && arrival > now ? arrival : now;

        return from + Duration;
    }

    public static Expression<Func<BedAssignmentEntity, bool>> LiveOn(DateTimeOffset now)
        => assignment => assignment.Status == AssignmentStatus.Occupied
            || (assignment.Status == AssignmentStatus.Reserved
                && (assignment.ReservedUntil == null || assignment.ReservedUntil > now));

    public static bool IsLive(BedAssignmentEntity assignment, DateTimeOffset now)
        => assignment.Status == AssignmentStatus.Occupied
            || (assignment.Status == AssignmentStatus.Reserved
                && (assignment.ReservedUntil == null || assignment.ReservedUntil > now));
}
