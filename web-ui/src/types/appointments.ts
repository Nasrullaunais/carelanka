import type { AdmissionCategory, AppointmentStatus } from '../services/api/generated';

export const appointmentStatuses: AppointmentStatus[] = [
  'scheduled',
  'checked_in',
  'completed',
  'cancelled',
  'no_show',
];

export const appointmentStatusLabels: Record<AppointmentStatus, string> = {
  scheduled: 'Expected',
  checked_in: 'Checked in',
  completed: 'Finished',
  cancelled: 'Cancelled',
  no_show: 'Did not come',
};

export const deskCareLevels: AdmissionCategory[] = ['outpatient', 'day_case', 'inpatient'];

export const dutyManagerCareLevels: AdmissionCategory[] = [...deskCareLevels, 'hdu', 'icu'];
