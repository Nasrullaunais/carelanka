import type { MaintenanceStatus, MaintenanceType } from '../services/api/generated';

// UI-only labels, keyed off the generated enums so a new value on the API is a TypeScript
// error here rather than a blank cell on screen.

export const maintenanceTypeLabels: Record<MaintenanceType, string> = {
  routine_service: 'Routine service',
  calibration: 'Calibration',
  repair: 'Reported fault',
};

export const maintenanceStatusLabels: Record<MaintenanceStatus, string> = {
  scheduled: 'Waiting',
  in_progress: 'Being worked on',
  completed: 'Done',
  overdue: 'Overdue',
  cancelled: 'Cancelled',
};

// What the unit still has to do. Overdue is not stored, it is computed at read time from a
// scheduled job whose date has passed, so asking for scheduled work returns overdue work too.
export const openMaintenanceStatuses: MaintenanceStatus[] = ['scheduled', 'in_progress'];
