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

/** True when the ward gives more intensive care than the admission was filed at. */
export function isMoreAcuteThanNeeded(
  category: AdmissionCategory,
  wardType: WardType,
): boolean {
  return wardRung[wardType] < categoryRung[category];
}

/** The age from which a patient is an adult. Mirrors BedPlacementRules.PediatricAgeLimit. */
export const pediatricAgeLimit = 18;

/**
 * Whether the patient is under {@link pediatricAgeLimit} today.
 *
 * A missing date of birth is NOT a child, the same way an unknown gender reaches only a mixed
 * ward: the children's ward takes a recorded fact to earn, not the absence of one.
 */
export function isChild(dateOfBirth: string | null | undefined): boolean {
  if (!dateOfBirth) return false;

  const born = new Date(`${dateOfBirth}T00:00:00`);
  if (Number.isNaN(born.getTime())) return false;

  const today = new Date();
  let age = today.getFullYear() - born.getFullYear();

  // Their birthday has not come round yet this year.
  const beforeBirthday =
    today.getMonth() < born.getMonth() ||
    (today.getMonth() === born.getMonth() && today.getDate() < born.getDate());

  if (beforeBirthday) age--;

  return age < pediatricAgeLimit;
}

/**
 * What this person may do with this bed for this admission.
 *
 * Three answers rather than two, because "allowed" and "allowed but off the care level's own
 * path" need to look different on the screen. `override` is the duty manager spending a bed
 * the rules would not have picked — an ICU patient into an emergency bed when intensive care
 * is full — and the button for it is coloured so nobody does it by accident.
 */
export type Placement =
  | { kind: 'ok' }
  | { kind: 'override'; why: string }
  | { kind: 'refused'; why: string };

/**
 * Whether this bed can be chosen for this admission by this person, and on what terms.
 *
 * One sentence in the words of the job, not the words of the rule. The server's own message
 * is more precise; this is what stops the nurse ever reading it.
 */
export function placementFor(
  bed: { has_isolation: boolean },
  ward: { ward_type: WardType; gender_policy: GenderPolicy } | undefined,
  admission: { admission_category: AdmissionCategory; is_infectious: boolean },
  patient: { gender: Gender | undefined; date_of_birth?: string | null },
  role: PrincipalRole | undefined,
): Placement {
  // A bed whose ward we could not load. Saying so beats offering it and hoping.
  if (!ward) return { kind: 'refused', why: 'Ward not found' };

  // H3. Gender separation is a property of the ward, so there is no exception to make here.
  // A patient recorded as `other` or `unknown` reaches a mixed ward and nothing else.
  if (patient.gender && !acceptsGender(ward.gender_policy, patient.gender)) {
    return {
      kind: 'refused',
      why: ward.gender_policy === 'male' ? 'Male ward' : 'Female ward',
    };
  }

  // H6. Like the gender policy, a property of the ward — the duty manager has no override.
  if (ward.ward_type === 'pediatric' && !isChild(patient.date_of_birth)) {
    return {
      kind: 'refused',
      why: patient.date_of_birth
        ? `Children's ward — under ${pediatricAgeLimit}s only`
        : 'No date of birth recorded',
    };
  }

  // H4. The flag is on the bed, not the ward — a side room is not only found in isolation.
  if (admission.is_infectious && !bed.has_isolation) {
    return { kind: 'refused', why: 'Bed has no isolation' };
  }

  // Does this ward give the level of care the patient was assessed at? Null means yes, and yes
  // is the only answer that is a plain choice rather than a decision somebody has to own.
  const mismatch = isDowngrade(admission.admission_category, ward.ward_type)
    ? 'Lower care level than assessed'
    : isMoreAcuteThanNeeded(admission.admission_category, ward.ward_type)
      ? 'Higher care level than assessed'
      : null;

  // Mirrors BedPlacementRules.NeedsDutyManager, which is now the mismatch and nothing else. An
  // ICU ward is NOT special: for an ICU patient it is simply the right bed, and a nurse may
  // choose it. Only a ward the care level does not point at is somebody else's decision.
  //
  // Shown with its reason rather than hidden — a nurse looking at a bed she cannot take needs
  // to know it is there and who to ask.
  if (mismatch !== null && role !== 'duty_manager') {
    return { kind: 'refused', why: `${mismatch} — duty manager only` };
  }

  // Amber is exactly the off-path case, which is also exactly the duty manager's speciality.
  return mismatch === null ? { kind: 'ok' } : { kind: 'override', why: mismatch };
}
