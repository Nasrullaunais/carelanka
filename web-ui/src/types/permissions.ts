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
