import type { GenderPolicy, WardType } from '../services/api/generated';

// UI-only concerns, keyed off the generated enums. Record<WardType, string> means adding a
// ward type to the API is a TypeScript error here rather than a blank cell in the table.

export const wardTypeLabels: Record<WardType, string> = {
  icu: 'ICU',
  hdu: 'High dependency',
  general: 'General',
  maternity: 'Maternity',
  pediatric: 'Pediatric',
  isolation: 'Isolation',
};

export const genderPolicyLabels: Record<GenderPolicy, string> = {
  male: 'Male only',
  female: 'Female only',
  mixed: 'Mixed',
};

export const wardTypes = Object.keys(wardTypeLabels) as WardType[];
export const genderPolicies = Object.keys(genderPolicyLabels) as GenderPolicy[];
