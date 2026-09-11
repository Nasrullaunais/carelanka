using System.Linq.Expressions;
using CareLanka.Api.Data.Enums;
using BedAssignmentEntity = CareLanka.Api.Data.Entities.Patient.BedAssignment;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The 30-minute hold on a bed, and the one definition of what still counts as a claim on one.
/// </summary>
/// <remarks>
/// patient-spec.yaml promises this in as many words — "that expiry logic lives here, in the
/// owning service, so no other component re-implements it differently". It started inside
/// <see cref="CapacityService"/>, which was the only reader. Step 6 added three more
/// (availability, manual assignment, and the occupancy answer Equipment asks for), so it moved
/// out into one place they all call rather than being copied into each of them.
///
/// Nothing here touches the database or the clock on its own. The caller takes one
/// <c>now</c> and passes it in, so every bed in one answer is judged against one instant —
/// read the clock per bed and a hold can lapse partway down a list.
/// </remarks>
public static class BedHold
{
    /// <summary>
    /// How long a hold stands. Thirty minutes, as patient-management-plan.md 5.3 publishes:
    /// long enough for a human to decide, short enough that a bed held for a patient who is
    /// not coming goes back to the pool without anyone having to notice.
    /// </summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// When a hold placed now should lapse: <c>(expected arrival or now) + 30 minutes</c>.
    /// </summary>
    /// <remarks>
    /// The <c>max</c> is not in the published formula and is not a change to it. An ambulance
    /// twenty minutes out needs a hold that outlives the journey, which is why the arrival time
    /// is used at all — but an arrival time already in the past would produce a hold that has
    /// expired before it is written, so a bed would be reserved and free in the same instant.
    /// Somebody overdue gets a fresh thirty minutes instead.
    ///
    /// **Open:** an arrival expected days away holds a bed for days, which is the opposite of
    /// what 5.3 wants from expiry. Nothing caps it today; a pre-registered visit that far out
    /// should probably not be reserving a bed at all.
    /// </remarks>
    public static DateTimeOffset ExpiresAt(DateTimeOffset? expectedArrival, DateTimeOffset now)
    {
        var from = expectedArrival is { } arrival && arrival > now ? arrival : now;

        return from + Duration;
    }

    /// <summary>
    /// What counts as a claim on a bed: somebody is in it, or a hold on it still stands.
    /// </summary>
    /// <remarks>
    /// A <c>reserved</c> row with no <c>reserved_until</c> counts as live. It should not exist:
    /// a hold with no expiry is a bed held forever. But if one ever does, the database index
    /// <c>ux_bed_assignments_live_bed</c> already treats it as claiming the bed, so reporting it
    /// free would offer a caller a bed the next INSERT then refuses. Under-reporting sends an
    /// ambulance one ward further; over-reporting sends it to a bed that is not there.
    /// </remarks>
    public static Expression<Func<BedAssignmentEntity, bool>> LiveOn(DateTimeOffset now)
        => assignment => assignment.Status == AssignmentStatus.Occupied
            || (assignment.Status == AssignmentStatus.Reserved
                && (assignment.ReservedUntil == null || assignment.ReservedUntil > now));

    /// <summary>
    /// The same question about a row already in memory, for callers that loaded the
    /// assignments with their admission rather than querying for them.
    /// </summary>
    /// <remarks>
    /// The condition is written twice, and it has to be. <see cref="LiveOn"/> is an expression
    /// because EF turns it into SQL, and EF cannot translate a method call inside a query — the
    /// same reason the urgency sort in AdmissionService is written inline. So: two spellings of
    /// one rule, kept side by side in one file so a change to either is a change to both in the
    /// same edit. **If you change one, change the other.**
    /// </remarks>
    public static bool IsLive(BedAssignmentEntity assignment, DateTimeOffset now)
        => assignment.Status == AssignmentStatus.Occupied
            || (assignment.Status == AssignmentStatus.Reserved
                && (assignment.ReservedUntil == null || assignment.ReservedUntil > now));
}
