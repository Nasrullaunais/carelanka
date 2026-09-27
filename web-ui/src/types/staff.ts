import type { CoverageStatus, LeaveStatus, LeaveType, StaffRole } from '../services/api/generated';

export const staffRoleLabels: Record<StaffRole, string> = {
  ward_nurse: 'Ward nurse',
  doctor: 'Doctor',
  ambulance_crew: 'Ambulance crew',
  general_staff: 'General staff',
  duty_manager: 'Duty manager',
  hospital_administrator: 'Hospital administrator',
  equipment_manager: 'Equipment manager',
};

export const staffRoles: StaffRole[] = [
  'ward_nurse',
  'doctor',
  'ambulance_crew',
  'general_staff',
  'duty_manager',
  'hospital_administrator',
  'equipment_manager',
];

export const staffRoleHints: Record<StaffRole, string> = {
  ward_nurse: 'Patient care, bed assignments, admission vitals, and shift duties.',
  doctor: 'Clinical diagnoses, prescriptions, treatment orders, and discharge clearances.',
  ambulance_crew: 'Emergency ambulance dispatch response and patient transit.',
  general_staff: 'Hospital reception, patient intake registration, and general operations.',
  duty_manager: 'Ward oversight, emergency management, bed coordination, and shift rosters.',
  hospital_administrator: 'System administration, staff management, wards, and billing policies.',
  equipment_manager: 'Medical equipment inventory, maintenance scheduling, and pharmacy items.',
};

export const coverageStatusLabels: Record<CoverageStatus, string> = {
  adequate: 'Adequate',
  at_minimum: 'At minimum',
  understaffed: 'Understaffed',
  critical: 'Critical',
};

export const coverageStatusTones: Record<CoverageStatus, string> = {
  adequate: 'badge',
  at_minimum: 'badge severity-low',
  understaffed: 'badge severity-high',
  critical: 'badge severity-critical',
};

export const leaveStatusLabels: Record<LeaveStatus, string> = {
  pending: 'Pending review',
  approved: 'Approved',
  rejected: 'Rejected',
  withdrawn: 'Withdrawn',
};

export const leaveStatusTones: Record<LeaveStatus, string> = {
  pending: 'badge severity-low',
  approved: 'badge',
  rejected: 'badge severity-critical',
  withdrawn: 'badge retired',
};

export const leaveTypeLabels: Record<LeaveType, string> = {
  annual: 'Annual leave',
  sick: 'Sick leave',
  emergency: 'Emergency leave',
  shift_swap: 'Shift swap',
};

export const leaveStatuses: LeaveStatus[] = ['pending', 'approved', 'rejected', 'withdrawn'];
export const leaveTypes: LeaveType[] = ['annual', 'sick', 'emergency', 'shift_swap'];
