

export const maxAgeYears = 120;

const nicPattern = /^(\d{9}[VvXx]|\d{12}|[A-Za-z][A-Za-z0-9]{5,14})$/;

const twelveDigitsOnlyFromYear = 2000;

const phonePattern = /^(0\d{9}|\+94\d{9})$/;

export const fieldLimits = {
  fullName: 200,
  address: 300,
  contactName: 200,
} as const;

export function nicProblem(value: string): string | null {
  const trimmed = value.trim();

  if (trimmed.length === 0) {
    return null;
  }

  if (!nicPattern.test(trimmed)) {
    return 'Nine digits and a V (199534501V), twelve digits (199745600321), or a passport number starting with a letter.';
  }

  const year = nicBirthYear(trimmed);
  const thisYear = new Date().getFullYear();

  if (year !== null && (year > thisYear || year < thisYear - maxAgeYears)) {
    return `An NIC starts with the birth year, and ${year} is not a possible one. Check the NIC.`;
  }

  return null;
}

const oldNicPattern = /^(\d{2})\d{7}[VvXx]$/;
const newNicPattern = /^(\d{4})\d{8}$/;

// Mirrors SriLankanNic on the server. The nine-digit form gives the last two digits of a 19xx
// year and was never issued to anyone born from 2000 on; the twelve-digit form gives the full
// year. A passport number carries no year, so this returns null for one.
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

  if (trimmedNic.length === 0 || birthYear === null || nicProblem(trimmedNic) !== null) {
    return null;
  }

  const expected = nicBirthYear(trimmedNic);

  if (expected === null) {
    return null;
  }

  if (oldNicPattern.test(trimmedNic) && birthYear >= twelveDigitsOnlyFromYear) {
    return 'Someone born in 2000 or later has a twelve-digit NIC, not nine digits and a V or X. Check the NIC and the date of birth.';
  }

  if (expected === birthYear) {
    return null;
  }

  return `The NIC gives a birth year of ${expected}, but the date of birth is in ${birthYear}. Check both.`;
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
