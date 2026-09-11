import type { PrincipalRole } from '../services/api/generated';
import {
  canReadPatientDetails,
  canReadCapacity,
  canReadEquipment,
  canReadWards,
  canRegisterPatient,
  canWorkAppointmentDesk,
  canWorkBillingDesk,
  canWorkDischargeChecklist,
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
    canAccess: canReadPatientDetails,
  },
  {
    to: '/appointments',
    label: 'Expected visits',
    description: 'Who has booked to come in. Take a booking, or check someone in.',
    canAccess: canWorkAppointmentDesk,
  },
  {
    to: '/discharge',
    label: 'Discharge',
    description: 'Who could go home, what is still outstanding, and the sign-off that sends them.',
    canAccess: canWorkDischargeChecklist,
  },
  {
    to: '/billing',
    label: 'Billing',
    description: 'What a visit costs, taking the money, and a bill to hand across the counter.',
    canAccess: canWorkBillingDesk,
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
  {
    to: '/pharmacy',
    label: 'Pharmacy',
    description: 'Search medicines and supplies, see what is on the shelf, record what moves.',
    canAccess: canReadEquipment,
  },
];

/** Only what this role may actually open. A tile they cannot use is worse than no tile. */
export function destinationsFor(role: PrincipalRole | undefined): Destination[] {
  return destinations.filter((destination) => destination.canAccess(role));
}
