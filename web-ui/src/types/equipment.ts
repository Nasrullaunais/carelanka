import type { EquipmentStatus, WarningSeverity } from '../services/api/generated';

export const equipmentStatusLabels: Record<EquipmentStatus, string> = {
  available: 'Available',
  assigned: 'In use',
  maintenance: 'Maintenance',
  retired: 'Retired',
};

export const equipmentStatuses = Object.keys(equipmentStatusLabels) as EquipmentStatus[];

export const editableTransitions: Record<EquipmentStatus, EquipmentStatus[]> = {
  available: ['maintenance', 'retired'],
  assigned: ['available'],
  maintenance: ['retired'],
  retired: [],
};

export const warningSeverityLabels: Record<WarningSeverity, string> = {
  low: 'Low',
  medium: 'Medium',
  high: 'High',
  critical: 'Critical',
};
