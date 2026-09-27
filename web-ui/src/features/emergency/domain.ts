import type { ChipVariants } from '@heroui/styles';
import type {
  AmbulanceEligibilityBlockReason,
  AmbulanceStatus,
  CallPriority,
  CallStatus,
  CancellationRequestStatus,
  CancelReason,
  DispatchOutcome,
  DispatchProposalStatus,
  DispatchRejectionReason,
  DispatchStatus,
} from '../../services/api/generated';

export type StatusTone = ChipVariants['color'];

export const priorityLabels: Record<CallPriority, string> = {
  critical: 'Critical',
  high: 'High',
  medium: 'Medium',
  low: 'Low',
};

export const priorityTones: Record<CallPriority, StatusTone> = {
  critical: 'danger',
  high: 'warning',
  medium: 'default',
  low: 'default',
};

export const callStatusLabels: Record<CallStatus, string> = {
  received: 'Received',
  dispatched: 'Dispatched',
  en_route: 'En route',
  completed: 'Completed',
  cancelled: 'Cancelled',
};

export const callStatusTones: Record<CallStatus, StatusTone> = {
  received: 'warning',
  dispatched: 'accent',
  en_route: 'accent',
  completed: 'success',
  cancelled: 'danger',
};

export const ambulanceStatusLabels: Record<AmbulanceStatus, string> = {
  available: 'Available',
  dispatched: 'Dispatched',
  en_route: 'En route',
  at_scene: 'At scene',
  transporting: 'Transporting',
  out_of_service: 'Out of service',
};

export const ambulanceStatusTones: Record<AmbulanceStatus, StatusTone> = {
  available: 'success',
  dispatched: 'warning',
  en_route: 'accent',
  at_scene: 'accent',
  transporting: 'accent',
  out_of_service: 'danger',
};

export const dispatchStatusLabels: Record<DispatchStatus, string> = {
  assigned: 'Assigned',
  acknowledged: 'Acknowledged',
  en_route_to_scene: 'En route to scene',
  at_scene: 'At scene',
  transporting_to_hospital: 'Transporting to hospital',
  handed_over: 'Handed over',
  declined: 'Declined',
  cancelled: 'Cancelled',
  reassigned: 'Reassigned',
};

export const dispatchStatusTones: Record<DispatchStatus, StatusTone> = {
  assigned: 'warning',
  acknowledged: 'accent',
  en_route_to_scene: 'accent',
  at_scene: 'accent',
  transporting_to_hospital: 'accent',
  handed_over: 'success',
  declined: 'danger',
  cancelled: 'danger',
  reassigned: 'danger',
};

export const proposalStatusLabels: Record<DispatchProposalStatus, string> = {
  pending: 'Pending',
  pending_confirmation: 'Awaiting confirmation',
  pending_approval: 'Awaiting approval',
  approved: 'Approved',
  executed: 'Executed',
  rejected: 'Rejected',
  failed: 'Failed',
};

export const proposalStatusTones: Record<DispatchProposalStatus, StatusTone> = {
  pending: 'default',
  pending_confirmation: 'warning',
  pending_approval: 'danger',
  approved: 'success',
  executed: 'success',
  rejected: 'danger',
  failed: 'danger',
};

export const proposalOutcomeLabels: Record<DispatchOutcome, string> = {
  free_ambulance_proposed: 'Free ambulance proposed',
  diversion_proposed: 'Diversion proposed',
  no_ambulance_available: 'No ambulance available',
  failed: 'Failed',
};

export const rejectionReasonLabels: Record<DispatchRejectionReason, string> = {
  unsafe_diversion: 'Unsafe diversion',
  source_call_too_urgent_to_divert: 'Source call is too urgent to divert',
  ambulance_unsuitable: 'Ambulance unsuitable',
  handled_another_way: 'Handled another way',
  no_longer_needed: 'No longer needed',
  other: 'Other',
};

export const cancellationStatusLabels: Record<CancellationRequestStatus, string> = {
  pending: 'Pending review',
  approved: 'Approved',
  rejected: 'Rejected',
};

export const cancelReasonLabels: Record<CancelReason, string> = {
  diverted_to_other_hospital: 'Diverted to another hospital',
  false_alarm: 'False alarm',
  died_en_route: 'Patient died en route',
  patient_refused: 'Patient refused transport',
  no_show: 'No show',
};

export const blockReasonLabels: Record<AmbulanceEligibilityBlockReason, string> = {
  inactive: 'Retired',
  out_of_service: 'Out of service',
  insufficient_crew: 'Not enough current crew',
  active_dispatch: 'Already on a live dispatch',
  missing_location: 'No current location',
  stale_location: 'Location is stale',
};

export function formatWaiting(minutes?: number | null): string {
  return minutes == null ? '—' : `${minutes} min`;
}

export function formatKilometres(km?: number | null): string {
  return km == null ? '—' : `${km.toFixed(1)} km`;
}

export function formatDriveMinutes(minutes?: number | null): string {
  return minutes == null ? '—' : `${minutes} min drive`;
}

export function formatTimestamp(iso?: string | null): string {
  return iso ? new Date(iso).toLocaleString() : 'Unknown';
}
