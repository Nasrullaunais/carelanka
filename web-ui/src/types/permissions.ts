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
