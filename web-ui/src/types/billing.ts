import type { BillLineSource } from '../services/api/generated';

// Presentation only, keyed off the generated unions so a contract change is a compile error
// here rather than a blank label at run time.

export const billLineSourceLabels: Record<BillLineSource, string> = {
  admission_fee: 'Admission fee',
  bed_stay: 'Bed',
  manual: 'Added at the desk',
};

/**
 * Why a line is there, in one sentence, for the hint under the table. Reception is going to be
 * asked "what is this charge?" across a counter, and "it came from the bed assignment rows" is
 * not an answer they can give.
 */
export const billLineSourceHints: Record<BillLineSource, string> = {
  admission_fee: 'Worked out from the care level a clinician recorded.',
  bed_stay: 'Worked out from the time the patient spent in that bed.',
  manual: 'Typed in here. Nothing in the system records treatments, so these are entered by hand.',
};

/** Only a typed line can be taken off again. A generated one comes back on the next prepare. */
export function isRemovableLine(source: BillLineSource): boolean {
  return source === 'manual';
}

/**
 * Money, the way it is read out at a counter: `LKR 15,500.00`.
 *
 * Fixed to two decimals even on a round number, because a column of amounts that sometimes has
 * decimals and sometimes does not is hard to add up by eye — and somebody will add it up.
 */
export function money(amount: number, currency = 'LKR'): string {
  return `${currency} ${amount.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

/** Quantities are days or counts, so a whole number prints as one. */
export function quantity(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}

// ---------------------------------------------------------------------------
// The discharge checklist
// ---------------------------------------------------------------------------

// The keys the server publishes on Discharge.checklist. Listed in the order they get done
// rather than alphabetically — a nurse reads down this list.
export const checklistOrder = [
  'clinical_clearance',
  'medication_issued',
  'billing_settled',
  'follow_up_recorded',
  'transport_arranged',
] as const;

/**
 * Which boxes hold up a discharge, mirroring DischargeService.Mandatory.
 *
 * Only used before the server has written any rows — the checklist is created the first time
 * somebody ticks something, so a patient who has just arrived has boxes to show and no data
 * behind them. Once a row exists its own `mandatory` is what the screen reads.
 */
export const mandatoryChecklistItems = new Set<string>([
  'clinical_clearance',
  'medication_issued',
  'billing_settled',
]);

export const checklistLabels: Record<string, string> = {
  clinical_clearance: 'Cleared by a doctor',
  medication_issued: 'Medication issued',
  billing_settled: 'Bill settled',
  follow_up_recorded: 'Follow-up recorded',
  transport_arranged: 'Transport arranged',
};

/** Who does it, and anything the reader needs to know before pressing the button. */
export const checklistHints: Record<string, string> = {
  clinical_clearance:
    'A doctor only, and never anything automatic. Without this nobody goes home.',
  medication_issued: 'The ward nurse — what the patient takes with them.',
  billing_settled:
    'Ticked by settling the bill on the Billing screen, and by nothing else. There is no button here on purpose: the bill and this box are the same fact.',
  follow_up_recorded: 'The ward nurse. Optional — it does not hold up a discharge.',
  transport_arranged: 'The ward nurse. Optional — it does not hold up a discharge.',
};

/** Falls back to the wire value, so a sixth box added server-side still reads sensibly. */
export function checklistLabel(item: string): string {
  return checklistLabels[item] ?? item.replaceAll('_', ' ');
}
