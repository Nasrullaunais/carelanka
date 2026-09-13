import type {
  AdmissionCategory,
  AdmissionSource,
  AdmissionStatus,
  AdmissionUrgency,
  Gender,
} from '../services/api/generated';

export const genders: Gender[] = ['male', 'female', 'other', 'unknown'];

export const genderLabels: Record<Gender, string> = {
  male: 'Male',
  female: 'Female',
  other: 'Other',
  unknown: 'Not known',
};

export const admissionCategories: AdmissionCategory[] = [
  'icu',
  'hdu',
  'inpatient',
  'day_case',
  'outpatient',
];

export const admissionCategoryLabels: Record<AdmissionCategory, string> = {
  icu: 'ICU - intensive care',
  hdu: 'HDU - high dependency',
  inpatient: 'Inpatient - staying in',
  day_case: 'Day case - in and out today',
  outpatient: 'Outpatient - no bed needed',
};

export const admissionCategoryHints: Record<AdmissionCategory, string> = {
  icu: 'Life support or constant monitoring.',
  hdu: 'Needs watching more closely than a general ward can manage, but not intensive care.',
  inpatient: 'Admitted to a ward, staying at least one night.',
  day_case: 'A procedure today, home the same day. Still needs a bed for a few hours.',
  outpatient: 'Seen and sent home. No bed is held.',
};

export const admissionUrgencies: AdmissionUrgency[] = ['routine', 'urgent', 'emergency'];

export const admissionUrgencyLabels: Record<AdmissionUrgency, string> = {
  routine: 'Routine - can wait',
  urgent: 'Urgent - seen today',
  emergency: 'Emergency - seen now',
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
  emergency_contact_name: 'Emergency contact name',
  emergency_contact_phone: 'Emergency contact phone',
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
