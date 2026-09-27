using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

public static class AdmissionStatusMachine
{
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

            [AdmissionStatus.Discharged] = [],
            [AdmissionStatus.Cancelled] = []
        };

    public static IReadOnlyCollection<AdmissionStatus> MovesFrom(AdmissionStatus from) => Moves[from];

    public static bool IsLegal(AdmissionStatus from, AdmissionStatus to)
        => Moves[from].Contains(to);

    public static bool IsTerminal(AdmissionStatus status) => Moves[status].Length == 0;

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
