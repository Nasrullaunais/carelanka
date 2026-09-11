import type {
  AdmissionCategory,
  BedAvailability,
  Gender,
  GenderPolicy,
  PrincipalRole,
  WardType,
} from '../services/api/generated';

// UI-only concerns, keyed off the generated enums. Record<BedAvailability, string> means a new
// availability value in the API is a TypeScript error here rather than a blank cell.
//
// The rules below MIRROR api/Services/Patient/BedPlacementRules.cs. They do not replace it:
// the server refuses an illegal placement whatever this file says, and every refusal arrives
// as a toast with its own message code. This exists so a nurse is not offered a bed that is
// going to be refused — thirty free beds and five silent 409s is not a picker.
//
// If BedPlacementRules changes, change this too. A picker that offers a bed the server will
// not take is worse than one that offers fewer beds.

export const bedAvailabilityLabels: Record<BedAvailability, string> = {
  free: 'Free',
  reserved: 'Held',
  occupied: 'Occupied',
  out_of_service: 'Out of service',
};

/**
 * How acute a care level is, as a step on the downgrade ladder. Lower is more intensive.
 *
 * `day_case` and `outpatient` sit on the same rung as `inpatient` because there is no ward
 * type below `general` — an ordinary bed is the floor.
 */
const categoryRung: Record<AdmissionCategory, number> = {
  icu: 0,
  hdu: 1,
  inpatient: 2,
  day_case: 2,
  outpatient: 2,
};

/** The most intensive care a ward of this kind can give, on the same ladder. */
const wardRung: Record<WardType, number> = {
  icu: 0,
  hdu: 1,
  general: 2,
  maternity: 2,
  pediatric: 2,
  isolation: 2,
  surgical: 2,
  emergency: 2,
  mental_health: 2,
};

/** Whether a ward with this policy takes a patient recorded as this gender. */
function acceptsGender(policy: GenderPolicy, gender: Gender): boolean {
  if (policy === 'mixed') return true;
  return policy === gender;
}

/** True when the ward is a step down from the care level the admission was filed at. */
export function isDowngrade(category: AdmissionCategory, wardType: WardType): boolean {
  return wardRung[wardType] > categoryRung[category];
}

/**
 * Why this bed cannot be chosen for this admission by this person, or null when it can.
 *
 * One sentence in the words of the job, not the words of the rule. The server's own message
 * is more precise; this is what stops the nurse ever reading it.
 */
export function whyNotPlaceable(
  bed: { has_isolation: boolean },
  ward: { ward_type: WardType; gender_policy: GenderPolicy } | undefined,
  admission: { admission_category: AdmissionCategory; is_infectious: boolean },
  gender: Gender | undefined,
  role: PrincipalRole | undefined,
): string | null {
  // A bed whose ward we could not load. Saying so beats offering it and hoping.
  if (!ward) return 'Ward not found';

  // H2, upward. Refused for everybody, duty manager included: it is not generosity, it is the
  // last intensive-care bed spent on somebody who does not need it.
  if (wardRung[ward.ward_type] < categoryRung[admission.admission_category]) {
    return 'More acute than this patient needs';
  }

  // H3. Gender separation is a property of the ward, so there is no exception to make here.
  // A patient recorded as `other` or `unknown` reaches a mixed ward and nothing else.
  if (gender && !acceptsGender(ward.gender_policy, gender)) {
    return ward.gender_policy === 'male' ? 'Male ward' : 'Female ward';
  }

  // H4. The flag is on the bed, not the ward — a side room is not only found in isolation.
  if (admission.is_infectious && !bed.has_isolation) {
    return 'No isolation in this bed';
  }

  // Not a hard rule but the same outcome: the server answers 403 rather than 409. Shown as a
  // reason rather than hidden, because a ward nurse looking at an empty ICU needs to know the
  // beds are there and who to ask.
  if (role !== 'duty_manager') {
    if (isDowngrade(admission.admission_category, ward.ward_type)) {
      return 'A downgrade — duty manager only';
    }

    if (ward.ward_type === 'icu' || ward.ward_type === 'hdu') {
      return 'Duty manager only';
    }
  }

  return null;
}
