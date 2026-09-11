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
