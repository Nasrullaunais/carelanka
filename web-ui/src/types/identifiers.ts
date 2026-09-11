// What a patient's NIC, phone number and date of birth are allowed to look like.
//
// These MIRROR api/Services/Patient/PatientIdentifierFormats.cs. They do not replace it: the
// server refuses a bad value whatever this file says, and every refusal arrives as a 400 with
// the field named. This exists so a typist is told at the field instead — a form that accepts
// what you typed and then fails on submit makes you hunt for which of eight boxes was wrong.
//
// If PatientIdentifierFormats changes, change this too.

/** The oldest date of birth anyone will credibly type. Matches MaxAgeYears on the server. */
export const maxAgeYears = 120;

/**
 * A Sri Lankan NIC in either form, or a passport number.
 *
 * Old NIC: nine digits and a V or an X. New NIC: twelve digits. Passport: six to fifteen
 * letters and digits, with at least one letter — that last condition is what stops every
 * mistyped NIC being waved through as "a passport, presumably".
 */
const nicPattern = /^(\d{9}[VvXx]|\d{12}|(?=.*[A-Za-z])[A-Za-z0-9]{6,15})$/;

/** Ten digits from a leading zero, or the +94 form read off a phone that has roamed. */
const phonePattern = /^(0\d{9}|\+94\d{9})$/;

/** The longest each free-text field may be, matching the MaxLength on the request. */
export const fieldLimits = {
  fullName: 200,
  address: 300,
  contactName: 200,
} as const;

/**
 * What is wrong with this NIC, or null when nothing is.
 *
 * An empty value is fine and always has been: an unidentified arrival has no papers, and the
 * server gives them a temp reference instead. These rules say what a *filled-in* field has to
 * look like, never that it has to be filled in.
 */
export function nicProblem(value: string): string | null {
  const trimmed = value.trim();

  if (trimmed.length === 0 || nicPattern.test(trimmed)) {
    return null;
  }

  return 'Nine digits and a V (199534501V), twelve digits (199745600321), or a passport number.';
}

export function phoneProblem(value: string): string | null {
  const trimmed = value.trim();

  if (trimmed.length === 0 || phonePattern.test(trimmed)) {
    return null;
  }

  return 'Ten digits starting with 0, like 0771234567.';
}

/**
 * What is wrong with this date of birth, or null.
 *
 * Aimed at 1097 for 1997, not at demographics — which is why the far end is 120 years and not
 * a round 100. A hospital that refuses to register a 106-year-old is a worse bug than one that
 * accepts an implausible 119.
 */
export function dateOfBirthProblem(value: string): string | null {
  if (value.length === 0) {
    return null;
  }

  const parsed = new Date(`${value}T00:00:00`);

  if (Number.isNaN(parsed.getTime())) {
    return 'That is not a date.';
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  if (parsed > today) {
    return 'A date of birth cannot be in the future.';
  }

  const oldest = new Date(today);
  oldest.setFullYear(oldest.getFullYear() - maxAgeYears);

  if (parsed < oldest) {
    return `That is more than ${maxAgeYears} years ago. Check the year.`;
  }

  return null;
}

/** Every year a date of birth could sensibly fall in, newest first. */
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

/**
 * How many days that month has, so 31 February can never be chosen in the first place.
 * Month is 1-based. With no year yet, 29 is allowed — February might turn out to be a leap one.
 */
export function daysInMonth(year: number | null, month: number | null): number {
  if (month === null) {
    return 31;
  }

  if (year === null) {
    return month === 2 ? 29 : new Date(2000, month, 0).getDate();
  }

  return new Date(year, month, 0).getDate();
}

/** The three pickers joined back into the `YYYY-MM-DD` the API takes, or '' while incomplete. */
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
