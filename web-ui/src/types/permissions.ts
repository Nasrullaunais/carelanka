import type { PrincipalRole } from '../services/api/generated';

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

export function canCreateWard(role: PrincipalRole | undefined): boolean {
  return role === 'hospital_administrator';
}

export function canReadWards(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

export function canReadEquipment(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

export function canManageEquipment(role: PrincipalRole | undefined): boolean {
  return role === 'equipment_manager';
}

export function canReportFault(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

export function canReadLabReports(role: PrincipalRole | undefined): boolean {
  return (
    role === 'doctor' ||
    role === 'ward_nurse' ||
    role === 'duty_manager' ||
    role === 'equipment_manager'
  );
}

// Narrower than canReadLabReports on purpose: a nurse reads a result and acts on it, and must
// not be able to file one nobody ran.
export function canFileLabReport(role: PrincipalRole | undefined): boolean {
  return role === 'equipment_manager';
}

export function canRegisterPatient(role: PrincipalRole | undefined): boolean {
  return role === 'general_staff' || role === 'ward_nurse' || role === 'duty_manager';
}

export function canEditPatient(role: PrincipalRole | undefined): boolean {
  return canRegisterPatient(role);
}

export function canReadPatientDetails(role: PrincipalRole | undefined): boolean {
  return isStaff(role) && role !== 'ambulance_crew';
}

export function canEditAdmissions(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

export function canWorkAppointmentDesk(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/// Who may open the bookings list. Wider than who may act on a booking: the
/// billing desk has to reach a finished appointment to bill it, but cannot
/// check anyone in. Mirrors `canOpenDischargeBoard`.
export function canOpenAppointmentBoard(role: PrincipalRole | undefined): boolean {
  return canWorkAppointmentDesk(role) || canWorkBillingDesk(role);
}

export function canSetHighCareLevel(role: PrincipalRole | undefined): boolean {
  return role === 'duty_manager';
}

export function canAssignBed(role: PrincipalRole | undefined): boolean {
  return role === 'general_staff' || role === 'ward_nurse' || role === 'duty_manager';
}

export function canWorkDischargeChecklist(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'doctor' || role === 'duty_manager';
}

export function canOpenDischargeBoard(role: PrincipalRole | undefined): boolean {
  return canWorkDischargeChecklist(role) || canWorkBillingDesk(role);
}

export function canTickChecklistItem(
  role: PrincipalRole | undefined,
  item: string,
): boolean {
  return item === 'clinical_clearance' && role === 'doctor';
}

export function canConfirmDischarge(role: PrincipalRole | undefined): boolean {
  return role === 'general_staff' || role === 'ward_nurse' || role === 'duty_manager';
}

export function canWorkBillingDesk(role: PrincipalRole | undefined): boolean {
  return (
    role === 'general_staff' ||
    role === 'hospital_administrator' ||
    role === 'duty_manager'
  );
}

export function canCompleteVisit(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

export function canMarkArrived(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse';
}

export function canReadCapacity(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

export function canSetBillingRates(role: PrincipalRole | undefined): boolean {
  return role === 'hospital_administrator';
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
