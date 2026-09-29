import type { AllocationEndReason, AllocationSource, AllocationStatus } from '../../../services/api/generated';

export const PAGE_SIZE = 15;

export const WEEKDAYS = [
  { code: 'mon', label: 'Mon' },
  { code: 'tue', label: 'Tue' },
  { code: 'wed', label: 'Wed' },
  { code: 'thu', label: 'Thu' },
  { code: 'fri', label: 'Fri' },
  { code: 'sat', label: 'Sat' },
  { code: 'sun', label: 'Sun' },
] as const;

export const allocationStatusLabels: Record<AllocationStatus, string> = {
  confirmed: 'Confirmed',
  proposed: 'Proposed',
  released: 'Released',
  cancelled: 'Cancelled',
};

export const allocationStatusTones: Record<AllocationStatus, string> = {
  confirmed: 'badge',
  proposed: 'badge severity-low',
  released: 'badge retired',
  cancelled: 'badge severity-critical',
};

export const allocationSourceLabels: Record<AllocationSource, string> = {
  manual: 'Manual',
  agent_proposal: 'AI Proposal',
  swap_request: 'Swap request',
};

export const allocationEndReasonLabels: Record<AllocationEndReason, string> = {
  manual: 'Manual reassignment',
  leave_approved: 'Leave approved',
  swapped_out: 'Swapped out',
  shift_cancelled: 'Shift cancelled',
  staff_deactivated: 'Staff deactivated',
};

export const ALLOCATION_END_REASONS: Array<{ value: AllocationEndReason; label: string }> = [
  { value: 'manual', label: 'Manual reassignment' },
  { value: 'leave_approved', label: 'Leave approved' },
  { value: 'swapped_out', label: 'Swapped out' },
  { value: 'shift_cancelled', label: 'Shift cancelled' },
  { value: 'staff_deactivated', label: 'Staff deactivated' },
];
