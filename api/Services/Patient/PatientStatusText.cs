using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The status of a stay or a booking, said in words a patient reads rather than in the
/// vocabulary the ward board uses.
/// </summary>
/// <remarks>
/// <b>Written here rather than in the app</b> so both frontends say the same sentence, and so
/// translating the app into Sinhala and Tamil is a change in one place. The enum stays on the
/// wire next to it — the app branches on <c>status</c> and displays this.
///
/// <b>No dates or times in any of these sentences.</b> Every timestamp in this API is UTC and
/// the server has no notion of a local timezone, so "Booked for 10:00" written here would read
/// 04:30 to a phone in Colombo. The app formats <c>scheduled_at</c> and <c>admitted_at</c>
/// itself, in the timezone the device is actually in. The phone knows that; the server does not.
/// </remarks>
public static class PatientStatusText
{
    /// <summary>Where their stay has got to.</summary>
    public static string For(AdmissionStatus status) => status switch
    {
        AdmissionStatus.AwaitingBed =>
            "A bed is being arranged for you.",

        AdmissionStatus.AwaitingApproval =>
            "A bed has been found for you and is waiting for a nurse to approve it.",

        // Deliberately does not name the bed. The ward and bed are their own fields on the
        // response, so a sentence repeating them would be one more place to keep in step.
        AdmissionStatus.BedReserved =>
            "A bed is being held for you.",

        AdmissionStatus.Admitted =>
            "You have been admitted.",

        AdmissionStatus.ReadyForDischarge =>
            "You are ready to go home. The ward is finishing your paperwork.",

        AdmissionStatus.Discharged =>
            "You have been discharged.",

        AdmissionStatus.Cancelled =>
            "This visit was cancelled.",

        // Unreachable while the enum and this switch agree. Left as a plain sentence rather
        // than a throw: a new status is a reason to show less detail, never a reason to fail
        // the one screen a patient uses to find out where they are.
        _ => "Ask at the ward for an update."
    };

    /// <summary>Where their booking has got to.</summary>
    public static string For(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled =>
            "Booked. You can still cancel this.",

        AppointmentStatus.CheckedIn =>
            "You have been checked in.",

        AppointmentStatus.Completed =>
            "This visit is finished.",

        AppointmentStatus.Cancelled =>
            "You cancelled this visit.",

        AppointmentStatus.NoShow =>
            "You did not attend this visit.",

        _ => "Ask at the desk for an update."
    };
}
