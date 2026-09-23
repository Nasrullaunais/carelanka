import type {
  AdmissionCategory,
  AdmissionSource,
  AdmissionStatus,
  Gender,
} from '../services/api/generated';

export const genders: Gender[] = ['male', 'female', 'other', 'unknown'];

export const selectableGenders: Gender[] = ['male', 'female'];

export const genderLabels: Record<Gender, string> = {
  male: 'Male',
  female: 'Female',
  other: 'Other',
  unknown: 'Not known',
};

export const admissionCategories: AdmissionCategory[] = [
  'icu',
  'general',
  'surgical',
  'maternity',
  'emergency',
];

export const admissionCategoryLabels: Record<AdmissionCategory, string> = {
  icu: 'ICU - life support / constant monitoring',
  general: 'General ward - normal admission',
  surgical: 'Surgical - pre- or post-operative care',
  maternity: 'Maternity - pregnancy and birth',
  emergency: 'Emergency - brief record, admit now',
};

export const admissionCategoryHints: Record<AdmissionCategory, string> = {
  icu: 'Life support or constant monitoring.',
  general: 'An ordinary ward stay.',
  surgical: 'Before or after an operation.',
  maternity: 'Pregnancy, birth, or postnatal care.',
  emergency:
    'Skips full registration - record just the NIC, admit, pick a bed. Fill in the rest later.',
};

export const admissionSourceLabels: Record<AdmissionSource, string> = {
  emergency: 'Ambulance',
  walk_in: 'Walk-in',
  pre_registered: 'Appointment',
};

export const admissionStatuses: AdmissionStatus[] = [
  'awaiting_bed',
  'awaiting_approval',
  'bed_reserved',
  'admitted',
  'ready_for_discharge',
  'discharged',
  'cancelled',
];

export const admissionStatusLabels: Record<AdmissionStatus, string> = {
  awaiting_bed: 'Awaiting bed',
  awaiting_approval: 'Awaiting approval',
  bed_reserved: 'Bed reserved',
  admitted: 'Admitted',
  ready_for_discharge: 'Ready for discharge',
  discharged: 'Discharged',
  cancelled: 'Cancelled',
};

export const patientDetailFieldLabels: Record<string, string> = {
  nic: 'NIC',
  date_of_birth: 'Date of birth',
  phone: 'Phone number',
  address: 'Address',
  emergency_contact_name: 'Emergency/guardian contact name',
  emergency_contact_phone: 'Emergency/guardian contact number',
};

export function detailFieldLabel(field: string): string {
  return patientDetailFieldLabels[field] ?? field.replaceAll('_', ' ');
}

export function patientIdentifier(patient: {
  nic?: string | null;
  temp_reference?: string | null;
}): string | null {
  return patient.nic ?? patient.temp_reference ?? null;
}
