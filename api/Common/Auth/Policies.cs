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

    // Patient Management combinations, taken from the "Roles:" line on each operation in
    // patient-spec.yaml. Named for what the holder may do rather than for the roles in them,
    // so adding a role later is one edit here and no edit in any controller.
    //
    // Added by Patient Management (M4) because build/common.md 7 says to ask rather than
    // write a role string in a controller. Rename or fold these into something broader if
    // the group prefers — they are additive and nothing else depends on the names yet.

    /// <summary>Registers patients at intake: ward nurse, ambulance crew, duty manager.</summary>
    public const string PatientRegistrar = nameof(PatientRegistrar);

    /// <summary>
    /// Reads patient records: ward nurse, duty manager, hospital administrator, doctor.
    /// A doctor reads and never edits - the record is kept by the people at the desk.
    /// </summary>
    public const string PatientReader = nameof(PatientReader);

    /// <summary>Edits a patient record: ward nurse, duty manager. Deliberately not the administrator.</summary>
    public const string PatientEditor = nameof(PatientEditor);

    /// <summary>Reads admissions: ward nurse, duty manager, doctor. Clinical work, so no administrator.</summary>
    public const string AdmissionReader = nameof(AdmissionReader);

    /// <summary>
    /// Completes the paperwork on an admission: ward nurse, duty manager.
    ///
    /// Split from <see cref="AdmissionReader"/> when doctors were given read access. Reading
    /// the worklist and filling in a patient's missing details are different jobs, and one
    /// policy over both would have handed every doctor the second along with the first.
    /// </summary>
    public const string AdmissionEditor = nameof(AdmissionEditor);

    /// <summary>
    /// Works the expected-visits desk — reads the worklist, books a visit on a patient's
    /// behalf, and checks them in: ward nurse, duty manager.
    /// </summary>
    /// <remarks>
    /// The same two roles as <see cref="AdmissionEditor"/> today, and deliberately a separate
    /// name. Taking a booking and filling in a patient's missing paperwork are different jobs,
    /// and one policy over both is how doctors nearly ended up with the paperwork when they
    /// were given read access to admissions.
    ///
    /// It does not cover the whole of check-in. `icu` and `hdu` are the duty manager's alone,
    /// and that rule depends on the request body rather than the route, so it lives in
    /// <c>AppointmentService</c> where the body is.
    /// </remarks>
    public const string AppointmentDesk = nameof(AppointmentDesk);
}
