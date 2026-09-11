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
 */
export function canRegisterPatient(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'ambulance_crew' || role === 'duty_manager';
}

/**
 * Policies.PatientReader on GET /api/patients and GET /api/patients/{id}.
 *
 * Deliberately not the same set as canRegisterPatient. Ambulance crew may register and look
 * up by NIC but may not browse the register, so intake must not offer them a name search.
 */
export function canReadPatients(role: PrincipalRole | undefined): boolean {
  return (
    role === 'ward_nurse' ||
    role === 'duty_manager' ||
    role === 'hospital_administrator' ||
    role === 'doctor'
  );
}

/** Policies.AdmissionReader on GET /api/admissions. Clinical work, so no administrator. */
export function canReadAdmissions(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager' || role === 'doctor';
}

/**
 * Policies.AdmissionEditor on PATCH /api/admissions/{id}/details.
 *
 * Narrower than canReadAdmissions on purpose: a doctor reads the worklist but does not chase
 * a patient's missing paperwork, which is desk work.
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
