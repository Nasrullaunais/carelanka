import type { PrincipalRole } from '../services/api/generated';
import {
  canReadPatientDetails,
  canReadCapacity,
  canReadEquipment,
  canReadWards,
  canRegisterPatient,
  canWorkAppointmentDesk,
  canOpenDischargeBoard,
  canSetBillingRates,
} from './permissions';

export type Destination = {
  to: string;
  label: string;

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
    label: 'Discharge and billing',
    description:
      'Who could go home: what they owe, what is still outstanding, and the sign-off that sends them.',
    canAccess: canOpenDischargeBoard,
  },
  {
    to: '/billing-settings',
    label: 'Billing settings',
    description: 'What the hospital charges: every expense, in every kind of ward.',
    canAccess: canSetBillingRates,
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
    to: '/maintenance-unit',
    label: 'Maintenance unit',
    description: 'Machines waiting to be fixed. Confirm a repair and the item goes back into service.',
    canAccess: canReadEquipment,
  },
  {
    to: '/pharmacy',
    label: 'Pharmacy',
    description: 'Search medicines and supplies, see what is on the shelf, record what moves.',
    canAccess: canReadEquipment,
  },
];

export function destinationsFor(role: PrincipalRole | undefined): Destination[] {
  return destinations.filter((destination) => destination.canAccess(role));
}
