import type {
  AdmissionCategory,
  BedAvailability,
  Gender,
  GenderPolicy,
  PrincipalRole,
  WardType,
} from '../services/api/generated';

export const bedAvailabilityLabels: Record<BedAvailability, string> = {
  free: 'Free',
  reserved: 'Held',
  occupied: 'Occupied',
  out_of_service: 'Out of service',
};

const categoryRung: Record<AdmissionCategory, number> = {
  icu: 0,
  general: 2,
  surgical: 2,
  maternity: 2,
  emergency: 2,
};

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

function acceptsGender(policy: GenderPolicy, gender: Gender): boolean {
  if (policy === 'mixed') return true;
  return policy === gender;
}

export function isDowngrade(category: AdmissionCategory, wardType: WardType): boolean {
  return wardRung[wardType] > categoryRung[category];
}

export function isMoreAcuteThanNeeded(
  category: AdmissionCategory,
  wardType: WardType,
): boolean {
  return wardRung[wardType] < categoryRung[category];
}

export const pediatricAgeLimit = 18;

export function isChild(dateOfBirth: string | null | undefined): boolean {
  if (!dateOfBirth) return false;

  const born = new Date(`${dateOfBirth}T00:00:00`);
  if (Number.isNaN(born.getTime())) return false;

  const today = new Date();
  let age = today.getFullYear() - born.getFullYear();

  const beforeBirthday =
    today.getMonth() < born.getMonth() ||
    (today.getMonth() === born.getMonth() && today.getDate() < born.getDate());

  if (beforeBirthday) age--;

  return age < pediatricAgeLimit;
}

export type Placement =
  | { kind: 'ok' }
  | { kind: 'override'; why: string }
  | { kind: 'refused'; why: string };

export function placementFor(
  bed: { has_isolation: boolean },
  ward: { ward_type: WardType; gender_policy: GenderPolicy } | undefined,
  admission: { admission_category: AdmissionCategory; is_infectious: boolean },
  patient: { gender: Gender | undefined; date_of_birth?: string | null },
  role: PrincipalRole | undefined,
): Placement {
  if (!ward) return { kind: 'refused', why: 'Ward not found' };

  if (patient.gender && !acceptsGender(ward.gender_policy, patient.gender)) {
    return {
      kind: 'refused',
      why: ward.gender_policy === 'male' ? 'Male ward' : 'Female ward',
    };
  }

  if (ward.ward_type === 'pediatric' && !isChild(patient.date_of_birth)) {
    return {
      kind: 'refused',
      why: patient.date_of_birth
        ? `Children's ward — under ${pediatricAgeLimit}s only`
        : 'No date of birth recorded',
    };
  }

  if (admission.is_infectious && !bed.has_isolation) {
    return { kind: 'refused', why: 'Bed has no isolation' };
  }

  const mismatch = isDowngrade(admission.admission_category, ward.ward_type)
    ? 'Lower care level than assessed'
    : isMoreAcuteThanNeeded(admission.admission_category, ward.ward_type)
      ? 'Higher care level than assessed'
      : null;

  if (mismatch !== null && role !== 'duty_manager') {
    return { kind: 'refused', why: `${mismatch} — duty manager only` };
  }

  return mismatch === null ? { kind: 'ok' } : { kind: 'override', why: mismatch };
}
