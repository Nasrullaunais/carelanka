import type {
  AdmissionCategory,
  AdmissionSource,
  AdmissionStatus,
  AdmissionUrgency,
  Gender,
} from '../services/api/generated';

// Presentation only. Every array is typed as the generated union, so deleting or renaming a
// value in the API turns into a compile error here rather than a blank dropdown at run time.

export const genders: Gender[] = ['male', 'female', 'other', 'unknown'];

export const genderLabels: Record<Gender, string> = {
  male: 'Male',
  female: 'Female',
  other: 'Other',
  // Not a polite refusal to ask. `unknown` is what an unconscious arrival is recorded as, and
  // the ward gender-policy filter is built to handle it deterministically.
  unknown: 'Not known',
};

// Ordered most to least intensive, the same order the enum declares, because that is the
// ladder the bed agent walks when it downgrades.
export const admissionCategories: AdmissionCategory[] = [
  'icu',
  'hdu',
  'inpatient',
  'day_case',
  'outpatient',
];

// This is the most consequential dropdown in the app: it decides what kind of bed the patient
// needs, it is the input to the bed agent's hard rules, and it is recorded against the name of
// whoever picked it. So it says what each level means rather than naming it and moving on.
export const admissionCategoryLabels: Record<AdmissionCategory, string> = {
  icu: 'ICU - intensive care',
  hdu: 'HDU - high dependency',
  inpatient: 'Inpatient - staying in',
  day_case: 'Day case - in and out today',
  outpatient: 'Outpatient - no bed needed',
};

/** Shown under the picker. One line each, in the words the desk would use. */
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
  pre_registered: 'Booked visit',
};

export const admissionStatusLabels: Record<AdmissionStatus, string> = {
  awaiting_bed: 'Awaiting bed',
  awaiting_approval: 'Awaiting approval',
  bed_reserved: 'Bed reserved',
  admitted: 'Admitted',
  ready_for_discharge: 'Ready for discharge',
  discharged: 'Discharged',
  cancelled: 'Cancelled',
};

// The server names what paperwork is outstanding rather than counting it, so the desk knows
// what to chase. These are the wire values of PatientDetailField.
export const patientDetailFieldLabels: Record<string, string> = {
  nic: 'NIC',
  date_of_birth: 'Date of birth',
  phone: 'Phone number',
  address: 'Address',
  emergency_contact_name: 'Emergency contact name',
  emergency_contact_phone: 'Emergency contact phone',
};

/** Falls back to the raw wire value, so a field added server-side still reads sensibly. */
export function detailFieldLabel(field: string): string {
  return patientDetailFieldLabels[field] ?? field.replaceAll('_', ' ');
}

/** What to call a patient who has no NIC: the generated temp reference, or nothing yet. */
export function patientIdentifier(patient: {
  nic?: string | null;
  temp_reference?: string | null;
}): string | null {
  return patient.nic ?? patient.temp_reference ?? null;
}
