import type { AdmissionCategory, AppointmentStatus } from '../services/api/generated';

export const appointmentStatuses: AppointmentStatus[] = [
  'scheduled',
  'confirmed',
  'completed',
  'cancelled',
  'no_show',
];

export const appointmentStatusLabels: Record<AppointmentStatus, string> = {
  scheduled: 'Needs confirming',
  confirmed: 'Expected',
  completed: 'Finished',
  cancelled: 'Cancelled',
  no_show: 'Did not come',
};

/// A confirmed booking is the only one with anything to do on it today, so it is
/// the only one that reads as live. The rest are either waiting on the desk or over.
export function appointmentStatusTone(status: AppointmentStatus): 'badge' | 'badge retired' {
  return status === 'confirmed' ? 'badge' : 'badge retired';
}

/// `completed` covers both endings, so the badge alone cannot say which. A row with an
/// admission behind it is a patient still in the building, and the desk needs to know.
export function appointmentOutcome(appointment: {
  status: AppointmentStatus;
  admission_id?: string | null;
}): string | null {
  if (appointment.status !== 'completed') return null;

  return appointment.admission_id
    ? 'Admitted to a ward'
    : 'Seen and went home';
}

export const deskCareLevels: AdmissionCategory[] = ['outpatient', 'day_case', 'inpatient'];

export const dutyManagerCareLevels: AdmissionCategory[] = [...deskCareLevels, 'hdu', 'icu'];
