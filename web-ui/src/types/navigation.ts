import type { PrincipalRole } from '../services/api/generated';
import {
  canReadPatientDetails,
  canReadCapacity,
  canReadEquipment,
  canRunMaintenance,
  canReadLabReports,
  canReadWards,
  canRegisterPatient,
  canOpenAppointmentBoard,
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
    description: 'Who has booked to come in, and when they are expected.',
    canAccess: canOpenAppointmentBoard,
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
    description: 'Book maintenance, confirm repairs done, and retire machines beyond repair.',
    canAccess: canRunMaintenance,
  },
  {
    to: '/laboratory',
    label: 'Laboratory',
    description: 'File a blood or lab result against a patient, and read what has been filed already.',
    canAccess: canReadLabReports,
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
