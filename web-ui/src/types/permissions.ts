import type { PrincipalRole } from '../services/api/generated';

// Mirrors the [Authorize(Policy = ...)] attributes on the controllers. The server is what
// actually enforces these — this exists so the UI can HIDE a control the user cannot use
// rather than offer it and let them find out from a 403.
//
// `patient` appears in PrincipalRole because the API issues it, not because this app serves
// it. Patients are a Flutter audience; nothing here should ever return true for them.

export const staffRoles: PrincipalRole[] = [
  'ward_nurse',
  'doctor',
  'ambulance_crew',
  'general_staff',
  'duty_manager',
  'hospital_administrator',
  'equipment_manager',
];

export function isStaff(role: PrincipalRole | undefined): boolean {
  return role !== undefined && role !== 'patient';
}

/** Policies.HospitalAdministrator on POST /api/wards. */
export function canCreateWard(role: PrincipalRole | undefined): boolean {
  return role === 'hospital_administrator';
}

/** Policies.AnyStaff on GET /api/wards. */
export function canReadWards(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

/** Policies.AnyStaff on GET /api/equipment-items and /api/equipment-categories. */
export function canReadEquipment(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

/** Policies.EquipmentManager on create, edit, assign and release. */
export function canManageEquipment(role: PrincipalRole | undefined): boolean {
  return role === 'equipment_manager';
}

/**
 * Policies.AnyStaff on POST /api/equipment-items/{id}/report-fault. Deliberately wider
 * than canManageEquipment: the nurse at the bedside is who finds the fault, and making
 * them chase an equipment manager first is how a broken machine stays in service.
 */
export function canReportFault(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

/**
 * Policies.PatientRegistrar on POST /api/patients, POST /api/patients/lookup and
 * POST /api/admissions. Registering someone and admitting them are the same permission:
 * both are intake, and splitting them would let a role start a job it cannot finish.
 *
 * Ambulance crew came off this list on 2026-09-11 and general staff went on. The crew are the
 * emergency response team; the paperwork happens at the hospital desk.
 */
export function canRegisterPatient(role: PrincipalRole | undefined): boolean {
  return role === 'general_staff' || role === 'ward_nurse' || role === 'duty_manager';
}

/**
 * Policies.PatientDetails on GET /api/patients, GET /api/patients/{id}, GET /api/admissions,
 * GET /api/admissions/{id} and GET /api/patient-worklist.
 *
 * Six of the seven staff roles; ambulance crew is the one left out. This used to be two
 * helpers, mirroring two server policies, and the split was never real: a PatientDetail
 * carries the patient's admissions, so every reader could already see care level, urgency and
 * status through GET /patients/{id} whichever policy they were on.
 */
export function canReadPatientDetails(role: PrincipalRole | undefined): boolean {
  return isStaff(role) && role !== 'ambulance_crew';
}

/**
 * Policies.AdmissionEditor on PATCH /api/admissions/{id}/details.
 *
 * Narrower than canReadPatientDetails on purpose: a doctor reads the worklist but does not
 * chase a patient's missing paperwork, which is desk work.
 */
export function canEditAdmissions(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/**
 * Policies.AppointmentDesk on GET /api/appointments, POST /api/appointments and
 * POST /api/appointments/{id}/check-in.
 *
 * The same two roles as canEditAdmissions today, kept as its own helper for the same reason
 * the server keeps it as its own policy: taking a booking and chasing paperwork are different
 * jobs, and one name over both is how a role quietly gains the other.
 */
export function canWorkAppointmentDesk(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/**
 * The half of check-in that is NOT a policy. AppointmentService refuses `icu` and `hdu` from
 * anyone but the duty manager, and it has to be checked there rather than on the route because
 * the rule reads the request body. Here it decides which care levels the picker offers, so a
 * nurse never chooses one and finds out from a 403.
 */
export function canSetHighCareLevel(role: PrincipalRole | undefined): boolean {
  return role === 'duty_manager';
}

/**
 * Policies.AdmissionEditor on POST /api/admissions/{id}/assign-bed.
 *
 * Only half the rule. Which *bed* a role may choose depends on the ward it stands in, so
 * BedAssignmentService refuses ICU, HDU and any downgrade from anyone but the duty manager and
 * answers 403 at run time. `whyNotPlaceable` in types/beds.ts is the UI half of that, and this
 * is only "may this person place patients at all".
 */
export function canAssignBed(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/**
 * Policies.DischargeChecklist on GET /api/discharges/candidates and
 * PATCH /api/discharges/{admissionId}/checklist.
 *
 * Wider than the roles that may tick any one box, because the boxes belong to different
 * people. canTickChecklistItem below is the per-box half.
 */
export function canWorkDischargeChecklist(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'doctor' || role === 'duty_manager';
}

/**
 * Which box this role may tick, mirroring DischargeService.TickedBy.
 *
 * `billing_settled` is nobody's: settling the bill writes it and the checklist endpoint
 * refuses the key outright, so the screen renders it as a state and never as a control.
 */
export function canTickChecklistItem(
  role: PrincipalRole | undefined,
  item: string,
): boolean {
  if (item === 'billing_settled') {
    return false;
  }

  return item === 'clinical_clearance' ? role === 'doctor' : role === 'ward_nurse';
}

/**
 * Policies.AdmissionEditor on POST /api/discharges/{admissionId}/confirm.
 *
 * Only half the rule, like canAssignBed. ICU and HDU discharges are the duty manager's, which
 * depends on the admission rather than the route, so DischargeService answers 403 at run time
 * and canConfirmDischargeOf below hides the button before that can happen.
 */
export function canConfirmDischarge(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/** The other half: a ward nurse confirms everything except ICU and HDU. */
export function canConfirmDischargeOf(
  role: PrincipalRole | undefined,
  category: string,
): boolean {
  if (!canConfirmDischarge(role)) {
    return false;
  }

  return role === 'duty_manager' || (category !== 'icu' && category !== 'hdu');
}

/**
 * Policies.BillingDesk on the four bill writes and GET /api/billing/outstanding.
 *
 * Reception is general staff and they are who takes money. The administrator is here because
 * they can already do everything at the desk, and the duty manager because there is nobody
 * else in the building at three in the morning.
 */
export function canWorkBillingDesk(role: PrincipalRole | undefined): boolean {
  return (
    role === 'general_staff' ||
    role === 'hospital_administrator' ||
    role === 'duty_manager'
  );
}

/**
 * Policies.AdmissionEditor on POST /api/admissions/{id}/complete.
 *
 * Only half the rule, like canAssignBed. The service also refuses a visit that *needs* a bed
 * with cl_pat_020 — finishing that one is a discharge, with a checklist and a bed to give
 * back. So the button is hidden on `requires_bed` rows as well as for the wrong role.
 */
export function canCompleteVisit(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/**
 * Policies.WardNurse on POST /api/admissions/{id}/arrive. Narrower than everything else on
 * this page: the nurse at the bedside is the one who can see the patient is in the bed.
 */
export function canMarkArrived(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse';
}

/**
 * Policies.AnyStaff on GET /api/capacity/wards and GET /api/wards/{id}/occupancy.
 *
 * Counts only, no patient identities, which is why it is open to every staff role — a porter
 * moving a bed and a dispatcher choosing a hospital both need the same number.
 */
export function canReadCapacity(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

export const roleLabels: Record<PrincipalRole, string> = {
  ward_nurse: 'Ward nurse',
  doctor: 'Doctor',
  ambulance_crew: 'Ambulance crew',
  general_staff: 'General staff',
  duty_manager: 'Duty manager',
  hospital_administrator: 'Hospital administrator',
  equipment_manager: 'Equipment manager',
  patient: 'Patient',
};
