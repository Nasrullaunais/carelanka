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

    public const string DischargeConfirmer = nameof(DischargeConfirmer);

    public const string DischargeChecklist = nameof(DischargeChecklist);

    public const string DischargeBoard = nameof(DischargeBoard);

    public const string BillingDesk = nameof(BillingDesk);

    public const string AppointmentDesk = nameof(AppointmentDesk);

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
}
