import type { AdmissionCategory, AppointmentStatus } from '../services/api/generated';

// Presentation only, keyed off the generated enums. Record<AppointmentStatus, string> means a
// status added to the API is a compile error here rather than a blank cell on the worklist.

export const appointmentStatuses: AppointmentStatus[] = [
  'scheduled',
  'checked_in',
  'completed',
  'cancelled',
  'no_show',
];

// Written for the desk, not for the schema. "Scheduled" is the wire value; "Expected today" is
// what the person reading the list is actually looking at.
export const appointmentStatusLabels: Record<AppointmentStatus, string> = {
  scheduled: 'Expected',
  checked_in: 'Checked in',
  completed: 'Finished',
  cancelled: 'Cancelled',
  no_show: 'Did not come',
};

/**
 * The care levels a ward nurse may choose at check-in.
 *
 * Mirrors AppointmentService.DutyManagerOnly, which refuses `icu` and `hdu` from anyone else.
 * Ordered least to most intensive here, the opposite of the intake list, because the desk
 * starts from "seen and sent home" and works up rather than down.
 */
export const deskCareLevels: AdmissionCategory[] = ['outpatient', 'day_case', 'inpatient'];

/** Everything above, plus the two the duty manager alone can authorise. */
export const dutyManagerCareLevels: AdmissionCategory[] = [...deskCareLevels, 'hdu', 'icu'];

/**
 * The yyyy-mm-dd the `?date=` filter wants, for the UTC day a moment falls in.
 *
 * `toISOString` is UTC, which is deliberate and NOT the same as the local date: the column
 * stores UTC and the endpoint filters whole UTC days. Colombo is 5½ hours ahead, so at 3am
 * local this is still yesterday's date — which is honest, because that is the day the server
 * would count the booking in.
 */
export function utcDay(when: Date): string {
  return when.toISOString().slice(0, 10);
}

/** The booked time in the reader's own timezone. Nobody at a desk reads UTC. */
export function localTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

/** Date and time together, for a list that is not filtered to one day. */
export function localDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** The value a `datetime-local` input wants: local wall-clock, no timezone, no seconds. */
export function localInputValue(when: Date): string {
  const offset = when.getTimezoneOffset() * 60_000;
  return new Date(when.getTime() - offset).toISOString().slice(0, 16);
}
