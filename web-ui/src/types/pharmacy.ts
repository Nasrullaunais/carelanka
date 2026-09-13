import type { PharmacyTransactionType } from '../services/api/generated';

export const transactionTypeLabels: Record<PharmacyTransactionType, string> = {
  received: 'Received',
  dispensed: 'Dispensed',
  adjusted: 'Adjusted',
  expired_removed: 'Expired, removed',
};

export const transactionTypes = Object.keys(
  transactionTypeLabels,
) as PharmacyTransactionType[];

export const takesStock: Record<PharmacyTransactionType, boolean> = {
  received: false,
  dispensed: true,
  adjusted: false,
  expired_removed: true,
};

export const needsNote: Record<PharmacyTransactionType, boolean> = {
  received: false,
  dispensed: false,
  adjusted: true,
  expired_removed: false,
};
