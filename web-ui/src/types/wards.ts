import type { GenderPolicy, WardType } from '../services/api/generated';

// UI-only concerns, keyed off the generated enums. Record<WardType, string> means adding a
// ward type to the API is a TypeScript error here rather than a blank cell in the table.

// Written for whoever is at the desk, not for a clinician. "Pediatric" and "HDU" are the
// words on the ward door, but they are not the words a receptionist reasons with, and this
// dropdown is where a patient gets put in the wrong kind of ward.
export const wardTypeLabels: Record<WardType, string> = {
  icu: 'ICU - intensive care',
  hdu: 'HDU - high dependency',
  general: 'General ward',
  maternity: 'Maternity',
  pediatric: "Children's ward",
  isolation: 'Isolation',
  surgical: 'Surgical ward',
  emergency: 'Emergency ward',
  mental_health: 'Mental health ward',
};

/** One line of plain English per ward type, shown under the picker rather than in it. */
export const wardTypeHints: Record<WardType, string> = {
  icu: 'The sickest patients. Constant monitoring, one nurse to one or two beds.',
  hdu: 'A step down from ICU. Closer watching than a general ward, short of intensive care.',
  general: 'The ordinary ward. Most inpatients go here.',
  maternity: 'Childbirth and immediate aftercare.',
  pediatric: 'Children. Age, not illness, is what puts someone here.',
  isolation: 'Infection risk. A patient who must not share air with the rest of the ward.',
  surgical: 'Before and after an operation. Ordinary beds, with the theatre next door.',
  emergency: 'The first few hours. Somebody who has just come in and is not yet placed.',
  mental_health: 'Psychiatric care. A ward, not a level of medical intensity.',
};

export const genderPolicyLabels: Record<GenderPolicy, string> = {
  male: 'Male only',
  female: 'Female only',
  mixed: 'Mixed',
};

export const wardTypes = Object.keys(wardTypeLabels) as WardType[];
export const genderPolicies = Object.keys(genderPolicyLabels) as GenderPolicy[];
