import type { PrincipalRole } from '../services/api/generated';
import {
  canReadAdmissions,
  canReadCapacity,
  canReadEquipment,
  canReadWards,
  canRegisterPatient,
  canWorkAppointmentDesk,
} from './permissions';

// Every place a staff member can go, in one list. The dashboard reads it and so does anything
// else that needs to know what a role can reach, so a new screen is one entry here rather than
// an edit in three files that quietly drift apart.

export type Destination = {
  to: string;
  label: string;
  /** One line saying what the job is, not what the screen is called. */
  description: string;
  canAccess: (role: PrincipalRole | undefined) => boolean;
};

export const destinations: Destination[] = [
  {
    to: '/intake',
    label: 'Walk-in intake',
    description: 'Someone has arrived at the desk. Look them up, register them, admit them.',
    canAccess: canRegisterPatient,
  },
  {
    to: '/patients',
    label: 'Patients',
    description: 'Who is in the hospital now, with their intake details and times.',
    canAccess: canReadAdmissions,
  },
  {
    to: '/appointments',
    label: 'Expected visits',
    description: 'Who has booked to come in. Take a booking, or check someone in.',
    canAccess: canWorkAppointmentDesk,
  },
  {
    to: '/capacity',
    label: 'Bed capacity',
    description: 'How many beds are free, ward by ward. Counts only, no patient details.',
    canAccess: canReadCapacity,
  },
  {
    to: '/wards',
    label: 'Wards',
    description: 'The ward register — what each ward is for and who it takes.',
    canAccess: canReadWards,
  },
  {
    to: '/equipment',
    label: 'Equipment',
    description: 'Every machine in the hospital, where it is, and whether it works.',
    canAccess: canReadEquipment,
  },
];

/** Only what this role may actually open. A tile they cannot use is worse than no tile. */
export function destinationsFor(role: PrincipalRole | undefined): Destination[] {
  return destinations.filter((destination) => destination.canAccess(role));
}
