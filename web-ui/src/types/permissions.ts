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

// The equipment manager registers an item and the hospital administrator confirms it on the
// mobile app. Both need to know how many are still waiting.
export function canTrackEquipmentConfirmations(role: PrincipalRole | undefined): boolean {
  return role === 'equipment_manager' || role === 'hospital_administrator';
}

// Only the hospital administrator, so nobody confirms an item they registered themselves. The API
// also wants the confirmation code on top of the role.
export function canConfirmEquipment(role: PrincipalRole | undefined): boolean {
  return role === 'hospital_administrator';
}

// The maintenance unit is the hospital administrator's: booking work, the open-jobs list, confirming
// repairs and retiring what cannot be fixed. The equipment manager only reports faults.
export function canRunMaintenance(role: PrincipalRole | undefined): boolean {
  return role === 'hospital_administrator';
}

// Low stock, expiring medicine and overdue maintenance: the equipment manager runs the pharmacy
// and the register, and the administrator runs the maintenance unit.
export function canReadWarnings(role: PrincipalRole | undefined): boolean {
  return role === 'equipment_manager' || role === 'hospital_administrator';
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
  return canRegisterPatient(role) || role === 'hospital_administrator';
}

export function canChangePatientIdentity(
  role: PrincipalRole | undefined,
  hasNic: boolean,
): boolean {
  return !hasNic || role === 'hospital_administrator';
}

export function canReadPatientDetails(role: PrincipalRole | undefined): boolean {
  return isStaff(role) && role !== 'ambulance_crew';
}

/// The medical profile the care advisory agent reads. The duty manager is on it because they
/// work the patients board; reception and the billing desk are not, and that is the whole point
/// of it being separate from canReadPatientDetails.
export function canReadMedicalProfile(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'doctor' || role === 'duty_manager';
}

/// Narrower than canReadMedicalProfile by the duty manager, who reads a ward board rather than
/// taking a clinical history.
export function canWriteMedicalProfile(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'doctor';
}

export function canEditAdmissions(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'duty_manager';
}

/// Reception is on this on purpose: booking, checking in, calling off and
/// recording a visit as seen is front-desk work, and the desk is who the
/// patient walks up to.
export function canWorkAppointmentDesk(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'general_staff' || role === 'duty_manager';
}

/// Who may open the bookings list. Wider than who may act on a booking: the
/// billing desk has to reach a finished appointment to bill it, but cannot
/// check anyone in. Mirrors `canOpenDischargeBoard`.
export function canOpenAppointmentBoard(role: PrincipalRole | undefined): boolean {
  return canWorkAppointmentDesk(role) || canWorkBillingDesk(role);
}

export function canManageEmergency(role: PrincipalRole | undefined): boolean {
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

/// An outpatient bill, not an admission one. Wider by the ward nurse: the
/// patient walks in and out the same day, so whoever records the visit as seen
/// is who takes the money for it. Settling an admission bill ticks the
/// discharge checklist and stays reception's alone.
export function canBillAppointment(role: PrincipalRole | undefined): boolean {
  return canWorkBillingDesk(role) || role === 'ward_nurse';
}

export function canMarkArrived(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'general_staff' || role === 'duty_manager';
}

export function canReadCapacity(role: PrincipalRole | undefined): boolean {
  return isStaff(role);
}

export function canSetBillingRates(role: PrincipalRole | undefined): boolean {
  return role === 'hospital_administrator';
}

/// The care advisory agent's review queue: what a patient reported and the agent's draft. The
/// duty manager can see the queue exists without being able to act on it - same split as
/// canReadMedicalProfile, which is exactly who may see what the agent read.
export function canReadCareQueue(role: PrincipalRole | undefined): boolean {
  return canReadMedicalProfile(role);
}

/// Approving, editing or rejecting a draft - the Doctor or the Ward Nurse on shift, the person
/// who will actually walk over and look at the patient. Narrower than canReadCareQueue.
export function canReviewCareRecommendation(role: PrincipalRole | undefined): boolean {
  return role === 'ward_nurse' || role === 'doctor';
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
