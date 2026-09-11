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

    /// <summary>
    /// Registers patients at intake: general staff, ward nurse, duty manager.
    /// </summary>
    /// <remarks>
    /// Ambulance crew was removed on 2026-09-11. They are the emergency response team and the
    /// paperwork is done at the hospital desk, not at the scene. That changes who creates the
    /// record for an unidentified casualty, which is Emergency's flow rather than ours - raised
    /// as an open item in integration_of_functions.md 11.9 for M1.
    /// </remarks>
    public const string PatientRegistrar = nameof(PatientRegistrar);

    /// <summary>
    /// Reads a patient and their visits: general staff, ward nurse, duty manager, hospital
    /// administrator, doctor, equipment manager. Six of the seven staff roles - ambulance crew
    /// is the one left out.
    /// </summary>
    /// <remarks>
    /// One policy where there were two. <c>PatientReader</c> and <c>AdmissionReader</c> were
    /// collapsed into this on 2026-09-11 because the split never actually held: a
    /// <c>PatientDetail</c> already carries the patient's admissions, so every
    /// <c>PatientReader</c> role could read care level, urgency and status through
    /// <c>GET /patients/{id}</c> whether or not they were on <c>AdmissionReader</c>. Two names
    /// over one real level of access is worse than one honest name.
    ///
    /// Doctor is in because <c>clinical_clearance</c> on the discharge checklist is theirs
    /// alone, and nobody can clear a discharge they cannot read. Equipment manager is in for
    /// the reason in integration_of_functions.md 11.8 - looking up a <c>patient_code</c> from
    /// their own assign screen.
    ///
    /// Reading only. Writing is <see cref="PatientEditor"/> and <see cref="AdmissionEditor"/>,
    /// both still ward nurse and duty manager alone.
    /// </remarks>
    public const string PatientDetails = nameof(PatientDetails);

    /// <summary>
    /// Edits a patient record: general staff, ward nurse, duty manager. Deliberately not the
    /// administrator, and deliberately the same three roles as <see cref="PatientRegistrar"/>.
    /// </summary>
    /// <remarks>
    /// Whoever may create a record may correct it. Splitting the two left reception able to
    /// register a patient and unable to fix the name thirty seconds later, which is not a
    /// safeguard — it is a typo that has to be chased through somebody else.
    /// </remarks>
    public const string PatientEditor = nameof(PatientEditor);

    /// <summary>
    /// Completes the paperwork on an admission: ward nurse, duty manager.
    ///
    /// Narrower than <see cref="PatientDetails"/> on purpose. A doctor opens a visit and an
    /// administrator reads the board; chasing a patient's missing address is desk work and
    /// belongs to the two roles that do it.
    /// </summary>
    public const string AdmissionEditor = nameof(AdmissionEditor);

    /// <summary>
    /// Works the discharge checklist: ward nurse, doctor, duty manager.
    /// </summary>
    /// <remarks>
    /// Wider than the three roles that may tick any one box, because the boxes belong to
    /// different people - <c>clinical_clearance</c> is the doctor's and the rest are the ward
    /// nurse's. Which one you may tick is <c>DischargeService</c>'s decision, since it depends
    /// on what is in the body rather than on the route.
    ///
    /// Confirming the discharge is not this policy. That is <see cref="AdmissionEditor"/>, one
    /// rung narrower, with ICU and HDU narrowing again to the duty manager inside the service.
    /// </remarks>
    public const string DischargeChecklist = nameof(DischargeChecklist);

    /// <summary>
    /// Reads the discharge board: ward nurse, doctor, duty manager, general staff, hospital
    /// administrator. Everyone who does <i>any</i> part of sending a patient home.
    /// </summary>
    /// <remarks>
    /// Wider than <see cref="DischargeChecklist"/> because the discharge screen is now the
    /// whole of a discharge - the bill is raised and settled on it, and reception is general
    /// staff. Without this, the one screen that does the job would be closed to the person who
    /// takes the money.
    ///
    /// <b>Reading only.</b> Every write on that screen keeps its own narrower policy:
    /// <c>clinical_clearance</c> is the doctor's, the bill is <see cref="BillingDesk"/>, and
    /// confirming is <see cref="AdmissionEditor"/>. A wider door onto the room does not widen
    /// what anybody may do in it.
    /// </remarks>
    public const string DischargeBoard = nameof(DischargeBoard);

    /// <summary>
    /// Takes money: general staff, hospital administrator, duty manager.
    /// </summary>
    /// <remarks>
    /// Reception is general staff, and they are who settles a bill - the hospital administrator
    /// creates wards and runs the organisation. The administrator stays on the list because
    /// they can already do everything at the desk, and the duty manager because there is nobody
    /// else in the building at three in the morning.
    ///
    /// Settling is the only way <c>billing_settled</c> is ever ticked, so this policy is the
    /// real gate on that checklist item - <c>PATCH /discharges/{id}/checklist</c> refuses the
    /// key outright.
    /// </remarks>
    public const string BillingDesk = nameof(BillingDesk);

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
