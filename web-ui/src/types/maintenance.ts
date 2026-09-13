import type { MaintenanceStatus, MaintenanceType } from '../services/api/generated';

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

export const openMaintenanceStatuses: MaintenanceStatus[] = ['scheduled', 'in_progress'];
