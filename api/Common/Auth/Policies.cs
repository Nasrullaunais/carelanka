namespace CareLanka.Api.Common.Auth;

public static class Policies
{
    public const string WardNurse = nameof(WardNurse);
    public const string Doctor = nameof(Doctor);
    public const string AmbulanceCrew = nameof(AmbulanceCrew);
    public const string GeneralStaff = nameof(GeneralStaff);
    public const string DutyManager = nameof(DutyManager);
    public const string HospitalAdministrator = nameof(HospitalAdministrator);
    public const string EquipmentManager = nameof(EquipmentManager);

    public const string AnyStaff = nameof(AnyStaff);
    public const string PatientOnly = nameof(PatientOnly);
    public const string WorkflowReader = nameof(WorkflowReader);
    public const string WorkflowStarter = nameof(WorkflowStarter);
    public const string EmergencyResponder = nameof(EmergencyResponder);

    public const string PatientRegistrar = nameof(PatientRegistrar);

    public const string PatientDetails = nameof(PatientDetails);

    public const string PatientEditor = nameof(PatientEditor);

    public const string AdmissionEditor = nameof(AdmissionEditor);

    public const string BedAssigner = nameof(BedAssigner);

    /// <summary>
    /// Confirming the patient is physically in the bed. The desk and the ward both see them
    /// arrive, so this is the same three roles as <see cref="BedAssigner"/> rather than the
    /// nurse alone.
    /// </summary>
    public const string ArrivalConfirmer = nameof(ArrivalConfirmer);

    public const string DischargeConfirmer = nameof(DischargeConfirmer);

    public const string DischargeChecklist = nameof(DischargeChecklist);

    public const string DischargeBoard = nameof(DischargeBoard);

    public const string BillingDesk = nameof(BillingDesk);

    /// <summary>
    /// Working an outpatient bill - the visit the patient walks in and out of on the same day.
    /// Wider than <see cref="BillingDesk"/> by the ward nurse, because the nurse who records
    /// the visit as seen is standing in front of the patient and is who takes the money for it.
    /// An admission bill stays on <see cref="BillingDesk"/>: it is settled at discharge, which
    /// ticks the discharge checklist, and that tick is reception's alone.
    /// </summary>
    public const string AppointmentBillingDesk = nameof(AppointmentBillingDesk);

    public const string AppointmentDesk = nameof(AppointmentDesk);

    /// <summary>
    /// Reading the bookings list. Wider than <see cref="AppointmentDesk"/>, which is who may
    /// act on a booking, because the billing desk has to reach a finished appointment to bill
    /// it and cannot check anyone in. Same split as
    /// <see cref="DischargeBoard"/> against <see cref="DischargeChecklist"/>.
    /// </summary>
    public const string AppointmentBoard = nameof(AppointmentBoard);

    // The laboratory stands on equipment_manager in both of these, because StaffRole has no
    // laboratory value. Adding one changes staff-spec.yaml and common-spec.yaml together, which
    // is M2's and the common owner's call - Open Decision 11. These two lines are all that
    // changes when it happens.
    public const string LabReportReader = nameof(LabReportReader);

    public const string LabReportAuthor = nameof(LabReportAuthor);

    // The same four roles as LabReportReader today, and deliberately a separate name: knowing
    // which ward somebody is in is what the laboratory and the equipment register both need to
    // offer a patient to pick, and neither of those is reading a test result.
    public const string PatientLocationReader = nameof(PatientLocationReader);

    // Somebody other than the equipment manager who registered an item, so nobody approves
    // their own entry. The confirmation code is checked by the service on top of this.
    public const string EquipmentConfirmer = nameof(EquipmentConfirmer);

    public const string EquipmentConfirmationTracker = nameof(EquipmentConfirmationTracker);

    // The maintenance unit is run by the hospital administrator: booking work, reading the work
    // list, and confirming it done. The equipment manager reports faults and nothing more here.
    public const string MaintenanceDesk = nameof(MaintenanceDesk);

    // Editing an item stays with the equipment manager; the administrator also needs it to retire a
    // machine the maintenance unit cannot fix.
    public const string EquipmentItemEditor = nameof(EquipmentItemEditor);
}
