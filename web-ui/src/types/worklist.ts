import type { WorklistRow, WorklistStatus } from '../services/api/generated';
import { admissionSourceLabels } from './patients';

export function arrivalRouteLabel(row: WorklistRow): string {
  return admissionSourceLabels[row.source ?? 'pre_registered'];
}

export const worklistStatusLabels: Record<WorklistStatus, string> = {
  not_arrived: 'Not arrived',
  awaiting_bed: 'Awaiting bed',
  bed_ready: 'Bed ready',
  admitted: 'Admitted',
  completed: 'Completed',
  cancelled: 'Cancelled',
};

export function worklistStatusDetail(row: WorklistRow): string | null {
  const bed = row.ward_name && row.bed_number ? `${row.ward_name} · ${row.bed_number}` : null;

  switch (row.status) {
    case 'bed_ready':
      return bed ? `${bed} — held` : 'Bed held';

    case 'admitted':
      if (bed) return bed;
      return row.requires_bed ? 'No bed recorded' : 'No bed needed';

    case 'awaiting_bed':
      return 'Needs a bed';

    case 'not_arrived':
      return null;

    default:
      return bed;
  }
}

export function worklistStatusTone(status: WorklistStatus): 'badge' | 'badge retired' {
  return status === 'admitted' || status === 'bed_ready' ? 'badge' : 'badge retired';
}
