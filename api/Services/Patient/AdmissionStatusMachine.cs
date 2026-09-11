using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The admission workflow, in one place. Every move between two statuses is decided here and
/// nowhere else, so there is exactly one answer to "can this patient go from here to there".
/// </summary>
/// <remarks>
/// Scattering the table across controllers is how a workflow ends up with two versions of
/// itself: the one the API enforces and the one the screens offer. Anything not on this table
/// is a 409, rejected rather than silently allowed.
/// </remarks>
public static class AdmissionStatusMachine
{
    // patient-management-plan.md 4.2 and the IllegalTransition response in patient-spec.yaml.
    // Three things are deliberate and not oversights:
    //
    //   - Both failure paths lead back to awaiting_bed. A rejected bed proposal and an expired
    //     hold land in the same place, so there is one waiting state rather than one error
    //     state per kind of failure.
    //   - admitted cannot be cancelled. You cannot call off a patient who is lying in your
    //     ward; they are discharged instead.
    //   - ready_for_discharge can go back to admitted. The checklist rule flagged them and a
    //     nurse looked and said no. That reversal is what keeps the flag advisory rather than
    //     binding.
    private static readonly IReadOnlyDictionary<AdmissionStatus, AdmissionStatus[]> Moves =
        new Dictionary<AdmissionStatus, AdmissionStatus[]>
        {
            [AdmissionStatus.AwaitingBed] =
            [
                AdmissionStatus.AwaitingApproval,
                AdmissionStatus.Cancelled
            ],
            [AdmissionStatus.AwaitingApproval] =
            [
                AdmissionStatus.BedReserved,
                AdmissionStatus.AwaitingBed,
                AdmissionStatus.Cancelled
            ],
            [AdmissionStatus.BedReserved] =
            [
                AdmissionStatus.Admitted,
                AdmissionStatus.AwaitingBed,
                AdmissionStatus.Cancelled
            ],
            [AdmissionStatus.Admitted] =
            [
                AdmissionStatus.ReadyForDischarge
            ],
            [AdmissionStatus.ReadyForDischarge] =
            [
                AdmissionStatus.Discharged,
                AdmissionStatus.Admitted
            ],

            // Terminal. A visit that ended stays ended — reopening one would rewrite history
            // and let a second open admission past ux_admissions_open_patient.
            [AdmissionStatus.Discharged] = [],
            [AdmissionStatus.Cancelled] = []
        };

    /// <summary>Where an admission in this status may go next. Empty for a status that has ended.</summary>
    public static IReadOnlyCollection<AdmissionStatus> MovesFrom(AdmissionStatus from) => Moves[from];

    /// <summary>True when the workflow has an edge from one status to the other. Staying put is not a move.</summary>
    public static bool IsLegal(AdmissionStatus from, AdmissionStatus to)
        => Moves[from].Contains(to);

    /// <summary>A status nothing leaves. The visit is over.</summary>
    public static bool IsTerminal(AdmissionStatus status) => Moves[status].Length == 0;

    /// <summary>
    /// Refuses, as a 409, a move this endpoint is not allowed to make from where the admission
    /// actually is.
    /// </summary>
    /// <param name="current">The status stored on the record right now.</param>
    /// <param name="to">The single move this endpoint performs.</param>
    /// <param name="from">The statuses <em>this endpoint</em> starts from.</param>
    /// <remarks>
    /// Two questions, not one, and both have to be yes.
    ///
    /// <paramref name="from"/> is the narrow one — which states this particular endpoint
    /// starts from. The table is the wide one. They differ: `ready_for_discharge -> admitted`
    /// is a legal move in the workflow (a nurse reversing a discharge flag), but
    /// `POST /admissions/{id}/arrive` is not the endpoint that makes it, because arriving
    /// stamps `admitted_at` and reversing a flag must not. Checking only the table would let
    /// one endpoint quietly do another's job; checking only <paramref name="from"/> would let
    /// a mistake in one argument list invent a move the workflow does not have.
    /// </remarks>
    public static void EnsureMove(
        AdmissionStatus current, AdmissionStatus to, params AdmissionStatus[] from)
    {
        if (from.Contains(current) && IsLegal(current, to))
        {
            return;
        }

        throw new IllegalTransitionException(
            "Admission", EnumWire.ToWire(current), EnumWire.ToWire(to));
    }
}
