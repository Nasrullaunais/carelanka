import type {
  RaisedBy,
  WarningSeverity,
  WarningStatus,
  WarningType,
} from '../services/api/generated';

export const warningTypes: WarningType[] = [
  'low_stock',
  'medicine_expiring',
  'maintenance_overdue',
  'equipment_faulty',
];

export const warningTypeLabels: Record<WarningType, string> = {
  low_stock: 'Low stock',
  medicine_expiring: 'Expiring medicine',
  maintenance_overdue: 'Maintenance overdue',
  equipment_faulty: 'Reported fault',
};

export const warningSeverityLabels: Record<WarningSeverity, string> = {
  low: 'Low',
  medium: 'Medium',
  high: 'High',
  critical: 'Critical',
};

// The tabs on the warnings page: what still needs somebody, and what has been dealt with.
export const warningStatuses: WarningStatus[] = ['open', 'acknowledged', 'action_taken', 'dismissed'];

export const warningStatusLabels: Record<WarningStatus, string> = {
  open: 'Open',
  acknowledged: 'Acknowledged',
  action_taken: 'Resolved',
  dismissed: 'Dismissed',
};

export const raisedByLabels: Record<RaisedBy, string> = {
  system: 'Automatic check',
  user: 'Staff report',
  agent: 'Agent',
};
