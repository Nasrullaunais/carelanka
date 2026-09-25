import { Ambulance, BedDouble, CalendarDays, ClipboardCheck, FlaskConical, HeartPulse, Hospital, Package, Pill, Settings2, ShieldAlert, UserPlus, Users, Wrench, type LucideIcon } from 'lucide-react';
import type { PrincipalRole } from '../services/api/generated';
import {
  canManageEmergency,
  canReadPatientDetails,
  canReadCapacity,
  canReadEquipment,
  canRunMaintenance,
  canReadWarnings,
  canReadLabReports,
  canReadWards,
  canRegisterPatient,
  canOpenAppointmentBoard,
  canOpenDischargeBoard,
  canSetBillingRates,
  canReadCareQueue,
} from './permissions';

export type Destination = {
  to: string;
  label: string;

  description: string;
  icon: LucideIcon;
  group: 'Patient care' | 'Hospital' | 'Administration';
  canAccess: (role: PrincipalRole | undefined) => boolean;
};

export const destinations: Destination[] = [
  { to: '/emergency', label: 'Emergency', description: 'Coordinate calls, ambulance dispatches, and emergency response.', icon: Ambulance, group: 'Hospital', canAccess: canManageEmergency },
  {
    to: '/intake',
    icon: UserPlus,
    group: 'Patient care',
    label: 'Walk-in intake',
    description: 'Someone has arrived at the desk. Look them up, register them, admit them.',
    canAccess: canRegisterPatient,
  },
  {
    to: '/patients',
    icon: Users,
    group: 'Patient care',
    label: 'Patients',
    description: 'Who is in the hospital now, with their intake details and times.',
    canAccess: canReadPatientDetails,
  },
  {
    to: '/appointments',
    icon: CalendarDays,
    group: 'Patient care',
    label: 'Expected visits',
    description: 'Who has booked to come in, and when they are expected.',
    canAccess: canOpenAppointmentBoard,
  },
  {
    to: '/discharge',
    icon: ClipboardCheck,
    group: 'Patient care',
    label: 'Discharge and billing',
    description:
      'Who could go home: what they owe, what is still outstanding, and the sign-off that sends them.',
    canAccess: canOpenDischargeBoard,
  },
  {
    to: '/care-recommendations',
    icon: HeartPulse,
    group: 'Patient care',
    label: 'Care recommendations',
    description:
      "What patients have reported about how they feel, and the agent's draft note for a nurse or doctor to check.",
    canAccess: canReadCareQueue,
  },
  {
    to: '/billing-settings',
    icon: Settings2,
    group: 'Administration',
    label: 'Billing settings',
    description: 'What the hospital charges: every expense, in every kind of ward.',
    canAccess: canSetBillingRates,
  },
  {
    to: '/capacity',
    icon: BedDouble,
    group: 'Hospital',
    label: 'Bed capacity',
    description: 'How many beds are free, ward by ward. Counts only, no patient details.',
    canAccess: canReadCapacity,
  },
  {
    to: '/wards',
    icon: Hospital,
    group: 'Hospital',
    label: 'Wards',
    description: 'The ward register — what each ward is for and who it takes.',
    canAccess: canReadWards,
  },
  {
    to: '/equipment',
    icon: Package,
    group: 'Hospital',
    label: 'Equipment',
    description: 'Every machine in the hospital, where it is, and whether it works.',
    canAccess: canReadEquipment,
  },
  {
    to: '/maintenance-unit',
    icon: Wrench,
    group: 'Administration',
    label: 'Maintenance unit',
    description: 'Book maintenance, confirm repairs done, and retire machines beyond repair.',
    canAccess: canRunMaintenance,
  },
  {
    to: '/warnings',
    icon: ShieldAlert,
    group: 'Administration',
    label: 'Warnings',
    description: 'Medicine running low or about to expire, and machines overdue for service.',
    canAccess: canReadWarnings,
  },
  {
    to: '/laboratory',
    icon: FlaskConical,
    group: 'Patient care',
    label: 'Laboratory',
    description: 'File a blood or lab result against a patient, and read what has been filed already.',
    canAccess: canReadLabReports,
  },
  {
    to: '/pharmacy',
    icon: Pill,
    group: 'Hospital',
    label: 'Pharmacy',
    description: 'Search medicines and supplies, see what is on the shelf, record what moves.',
    canAccess: canReadEquipment,
  },
];

export function destinationsFor(role: PrincipalRole | undefined): Destination[] {
  return destinations.filter((destination) => destination.canAccess(role));
}
