import type { WorklistRow, WorklistStatus } from '../services/api/generated';
import { admissionSourceLabels } from './patients';

// Presentation only, for the one Status column on the patients board.
//
// The board used to carry a Status column and a Bed column side by side, and between them they
// said almost nothing. "Awaiting bed" appeared against a patient in for a blood test who was
// never going to be given one, and the Bed column was empty for everybody who had not got one
// yet — two columns to say "no bed", neither of which said why.

/**
 * How this person is getting here: Walk-in, Ambulance or Appointment. Always one of the three.
 *
 * A booking has no `source` of its own — that field lives on the admission, and there is no
 * admission until somebody is checked in. But the route is already known, because making an
 * appointment *is* the route. So this answers "Appointment" rather than leaving the cell
 * empty or writing "not yet" in it: whether they have turned up is the Status column's job,
 * and saying it twice in two columns is how the old Status and Bed pair said nothing.
 */
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

/**
 * The second line under the badge: the one fact that explains the badge.
 *
 * Null where the badge says it all. A line that reads "Admitted" under a badge reading
 * "Admitted" is a line somebody has to read to learn nothing.
 */
export function worklistStatusDetail(row: WorklistRow): string | null {
  const bed = row.ward_name && row.bed_number ? `${row.ward_name} · ${row.bed_number}` : null;

  switch (row.status) {
    case 'bed_ready':
      // Named, because the whole job this status creates is "go to that bed". And the hold
      // lapses in thirty minutes, so it is the one row on the board with a clock on it.
      return bed ? `${bed} — held, collect them` : 'A bed is held';

    case 'admitted':
      // Three different situations under one badge, and the difference matters: in a bed, or
      // here for a test that needs no bed, or in a bed nobody has recorded.
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

/** Green for someone the hospital is actively dealing with, grey for everything else. */
export function worklistStatusTone(status: WorklistStatus): 'badge' | 'badge retired' {
  return status === 'admitted' || status === 'bed_ready' ? 'badge' : 'badge retired';
}
