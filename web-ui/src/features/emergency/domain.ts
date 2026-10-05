import type { ChipVariants } from '@heroui/styles';
import type {
  AmbulanceEligibilityBlockReason,
  AmbulanceStatus,
  CallPriority,
  CallStatus,
  CancellationRequestStatus,
  DispatchOutcome,
  DispatchRecommendationSource,
  DispatchRejectionReason,
  DispatchStatus,
  DispatchWithdrawalReason,
  EmergencyCallOutcome,
  EmergencyCallSummary,
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
  closed_at_scene: 'Ended at the scene',
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
  closed_at_scene: 'success',
  declined: 'danger',
  cancelled: 'danger',
  reassigned: 'danger',
};

export const withdrawalReasonLabels: Record<DispatchWithdrawalReason, string> = {
  call_changed: 'The call changed',
  dispatched_manually: 'Dispatched by hand',
  call_closed: 'The call was closed',
  ambulance_no_longer_available: 'The ambulance is no longer available',
};

export const proposalOutcomeLabels: Record<DispatchOutcome, string> = {
  free_ambulance_proposed: 'Free ambulance proposed',
  diversion_proposed: 'Diversion proposed',
  no_ambulance_available: 'No ambulance available',
  failed: 'Failed',
};

export const recommendationSourceLabels: Record<DispatchRecommendationSource, string> = {
  model: 'Chosen by Gemini',
  model_unavailable: 'Fastest-first rule (Gemini unavailable)',
  model_rejected: 'Fastest-first rule (Gemini pick rejected)',
};

export const recommendationSourceTones: Record<DispatchRecommendationSource, StatusTone> = {
  model: 'accent',
  model_unavailable: 'warning',
  model_rejected: 'warning',
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
  expired: 'Closed without a decision',
};

export const cancellationStatusTones: Record<CancellationRequestStatus, StatusTone> = {
  pending: 'warning',
  approved: 'success',
  rejected: 'danger',
  expired: 'default',
};

export const callOutcomeLabels: Record<EmergencyCallOutcome, string> = {
  transported: 'Taken to hospital',
  treated_at_scene: 'Treated at the scene',
  refused_transport: 'Patient refused transport',
  patient_not_found: 'Patient not found',
  deceased_at_scene: 'Patient died at the scene',
  false_alarm: 'False alarm',
  duplicate_call: 'Duplicate call',
  caller_cancelled: 'Caller cancelled',
  no_longer_needed: 'No longer needed',
};

// The four outcomes POST /emergency-calls/{id}/cancel accepts; the others belong to the crew.
export const callCloseOutcomes: EmergencyCallOutcome[] = ['false_alarm', 'duplicate_call', 'caller_cancelled', 'no_longer_needed'];

const prePickupStatuses: ReadonlySet<DispatchStatus> = new Set(['assigned', 'acknowledged', 'en_route_to_scene']);
const liveStatuses: ReadonlySet<DispatchStatus> = new Set([...prePickupStatuses, 'at_scene', 'transporting_to_hospital']);

export function isLiveDispatch(status?: DispatchStatus): boolean {
  return status !== undefined && liveStatuses.has(status);
}

export function isPrePickup(status?: DispatchStatus): boolean {
  return status !== undefined && prePickupStatuses.has(status);
}

export function isClosedCall(status?: CallStatus): boolean {
  return status === 'completed' || status === 'cancelled';
}

export const blockReasonLabels: Record<AmbulanceEligibilityBlockReason, string> = {
  inactive: 'Retired',
  out_of_service: 'Out of service',
  insufficient_crew: 'Not enough current crew',
  active_dispatch: 'Already on a live dispatch',
  missing_location: 'No current location',
  stale_location: 'Location is stale',
};

/** The place and its road, the first two parts of a geocoded address, which is what a dispatcher scans a list for. */
export function shortAddress(label: string): string {
  return label.split(',').map((part) => part.trim()).filter(Boolean).slice(0, 2).join(', ');
}

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

export function formatAge(iso?: string | null, now: number = Date.now()): string {
  const then = iso ? Date.parse(iso) : Number.NaN;
  if (Number.isNaN(then)) return 'never';
  const minutes = Math.max(0, Math.floor((now - then) / 60_000));
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  return `${Math.floor(minutes / 60)} hr ${minutes % 60} min ago`;
}

export interface RecommendationLine {
  label: string;
  tone: StatusTone;
}

export function awaitsDispatch(call: Pick<EmergencyCallSummary, 'status' | 'active_dispatch_id'>): boolean {
  return (call.status ?? 'received') === 'received' && !call.active_dispatch_id;
}

export function recommendationLine(call: EmergencyCallSummary): RecommendationLine | null {
  if (!awaitsDispatch(call)) return null;
  const proposal = call.latest_proposal;
  switch (proposal?.status) {
    case 'pending':
      return { label: 'Checking…', tone: 'default' };
    case 'pending_confirmation':
      return {
        label: proposal.proposed_ambulance_registration || 'Ready to send',
        tone: 'success',
      };
    case 'pending_approval':
      return { label: 'Diversion — needs approval', tone: 'warning' };
    default:
      return { label: 'Pick by hand', tone: 'danger' };
  }
}

const priorityRank: Record<CallPriority, number> = { low: 0, medium: 1, high: 2, critical: 3 };

export function compareByUrgency(left: EmergencyCallSummary, right: EmergencyCallSummary): number {
  return priorityRank[right.priority ?? 'high'] - priorityRank[left.priority ?? 'high']
    || (right.waiting_minutes ?? 0) - (left.waiting_minutes ?? 0);
}

export function nextCallToOpen(calls: EmergencyCallSummary[] | undefined, currentId: string): string | undefined {
  return (calls ?? []).filter((call) => call.id !== currentId && awaitsDispatch(call)).sort(compareByUrgency)[0]?.id;
}

export function emergencyCallPath(callId: string): string {
  return `/emergency/calls/${encodeURIComponent(callId)}`;
}
