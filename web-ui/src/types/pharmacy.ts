import type { PharmacyTransactionType } from '../services/api/generated';

// UI-only concerns, keyed off the generated enums. Record<PharmacyTransactionType, string>
// means adding a movement type to the API is a TypeScript error here rather than a blank
// cell in the history table.

export const transactionTypeLabels: Record<PharmacyTransactionType, string> = {
  received: 'Received',
  dispensed: 'Dispensed',
  adjusted: 'Adjusted',
  expired_removed: 'Expired, removed',
};

export const transactionTypes = Object.keys(
  transactionTypeLabels,
) as PharmacyTransactionType[];

// Which way each type moves the shelf. The server is what enforces this; the UI uses it to
// show a sign next to the quantity, because a bare "3" in a history row is unreadable.
//
// Quantity is always positive on the wire. The type is what gives it a sign, which is why
// this map exists at all rather than the number carrying it.
export const takesStock: Record<PharmacyTransactionType, boolean> = {
  received: false,
  dispensed: true,
  adjusted: false,
  expired_removed: true,
};

// Plan section 5.1: an adjustment is the one movement with no delivery or prescription
// behind it, so the server refuses it without a note. Mirrored here to disable the button
// rather than let the user find out from a 400.
export const needsNote: Record<PharmacyTransactionType, boolean> = {
  received: false,
  dispensed: false,
  adjusted: true,
  expired_removed: false,
};
