using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

public static class PatientStatusText
{
    public static string For(AdmissionStatus status) => status switch
    {
        AdmissionStatus.AwaitingBed =>
            "A bed is being arranged for you.",

        AdmissionStatus.AwaitingApproval =>
            "A bed has been found for you and is waiting for a nurse to approve it.",

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

        _ => "Ask at the ward for an update."
    };

    /// <param name="cancelledByHospital">
    /// Changes who the cancelled sentence blames. Telling a patient they
    /// cancelled a visit the desk called off is the kind of wrong that gets
    /// argued about at the counter.
    /// </param>
    public static string For(AppointmentStatus status, bool cancelledByHospital = false)
        => status switch
    {
        AppointmentStatus.Scheduled =>
            "Booked. The hospital will confirm it shortly, and you can still cancel.",

        AppointmentStatus.Confirmed =>
            "Confirmed by the hospital. You can still cancel this.",

        AppointmentStatus.Completed =>
            "This visit is finished.",

        AppointmentStatus.Cancelled => cancelledByHospital
            ? "The hospital cancelled this visit."
            : "You cancelled this visit.",

        AppointmentStatus.NoShow =>
            "You did not attend this visit.",

        _ => "Ask at the desk for an update."
    };
}
