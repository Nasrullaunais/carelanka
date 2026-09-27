

export const maxAgeYears = 120;

const nicPattern = /^(\d{9}[VvXx]|\d{12}|(?=.*[A-Za-z])[A-Za-z0-9]{6,15})$/;

const phonePattern = /^(0\d{9}|\+94\d{9})$/;

export const fieldLimits = {
  fullName: 200,
  address: 300,
  contactName: 200,
} as const;

export function nicProblem(value: string): string | null {
  const trimmed = value.trim();

  if (trimmed.length === 0 || nicPattern.test(trimmed)) {
    return null;
  }

  return 'Nine digits and a V (199534501V), twelve digits (199745600321), or a passport number.';
}

const oldNicPattern = /^(\d{2})\d{3}\d{3}\d[VvXx]$/;
const newNicPattern = /^(\d{4})\d{3}\d{5}$/;

// A Sri Lankan NIC encodes the birth year in its leading digits: two digits (assumed 19xx —
// the old format was retired before 2000) in the nine-digit form, four in the twelve-digit
// form. A passport number carries no such encoding, so this returns null for one.
export function nicBirthYear(nic: string): number | null {
  const trimmed = nic.trim();

  const oldMatch = oldNicPattern.exec(trimmed);
  if (oldMatch) {
    return 1900 + Number(oldMatch[1]);
  }

  const newMatch = newNicPattern.exec(trimmed);
  if (newMatch) {
    return Number(newMatch[1]);
  }

  return null;
}

export function nicBirthYearProblem(nic: string, birthYear: number | null): string | null {
  const trimmedNic = nic.trim();

  if (trimmedNic.length === 0 || birthYear === null) {
    return null;
  }

  const expected = nicBirthYear(trimmedNic);

  if (expected === null || expected === birthYear) {
    return null;
  }

  return `Doesn't match the NIC — its first digits say ${expected}, not ${birthYear}.`;
}

export function phoneProblem(value: string): string | null {
  const trimmed = value.trim();

  if (trimmed.length === 0 || phonePattern.test(trimmed)) {
    return null;
  }

  return 'Ten digits starting with 0, like 0771234567.';
}

export function dateOfBirthProblem(value: string): string | null {
  if (value.length === 0) {
    return null;
  }

  const parsed = new Date(`${value}T00:00:00`);

  if (Number.isNaN(parsed.getTime())) {
    return 'Not a valid date.';
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  if (parsed > today) {
    return 'A date of birth cannot be in the future.';
  }

  const oldest = new Date(today);
  oldest.setFullYear(oldest.getFullYear() - maxAgeYears);

  if (parsed < oldest) {
    return `More than ${maxAgeYears} years ago. Check the year.`;
  }

  return null;
}

export function birthYears(): number[] {
  const thisYear = new Date().getFullYear();

  return Array.from({ length: maxAgeYears + 1 }, (_, index) => thisYear - index);
}

export const monthNames = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

export function daysInMonth(year: number | null, month: number | null): number {
  if (month === null) {
    return 31;
  }

  if (year === null) {
    return month === 2 ? 29 : new Date(2000, month, 0).getDate();
  }

  return new Date(year, month, 0).getDate();
}

export function toIsoDate(
  year: number | null,
  month: number | null,
  day: number | null,
): string {
  if (year === null || month === null || day === null) {
    return '';
  }

  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}
