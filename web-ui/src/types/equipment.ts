import type { EquipmentStatus, WarningSeverity } from '../services/api/generated';

// UI-only concerns, keyed off the generated enums. Record<EquipmentStatus, string> means
// adding a status to the API is a TypeScript error here rather than a blank cell.

export const equipmentStatusLabels: Record<EquipmentStatus, string> = {
  available: 'Available',
  assigned: 'In use',
  maintenance: 'Maintenance',
  retired: 'Retired',
};

export const equipmentStatuses = Object.keys(equipmentStatusLabels) as EquipmentStatus[];

// Mirrors EnsureTransitionAllowed in EquipmentItemService, minus available -> assigned.
// That move is legal, but only the assign endpoint can make it, because assignment needs
// an admission id the edit form has no field for. Offering it here would earn a 400.
//
// The server is what enforces all of this. This exists so the UI offers only the moves
// that will succeed, rather than letting the user discover the rule from a 409.
export const editableTransitions: Record<EquipmentStatus, EquipmentStatus[]> = {
  available: ['maintenance', 'retired'],
  assigned: ['available'],
  maintenance: ['available', 'retired'],
  retired: [],
};

export const warningSeverityLabels: Record<WarningSeverity, string> = {
  low: 'Low',
  medium: 'Medium',
  high: 'High',
  critical: 'Critical',
};
