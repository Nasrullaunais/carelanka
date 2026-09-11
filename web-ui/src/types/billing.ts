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
// The price grid
// ---------------------------------------------------------------------------

/**
 * What each expense is called on the settings screen, keyed by the `expense_key` the API
 * publishes. These are the same keys as `chargeTemplates` below, on purpose: the price the
 * administrator sets here is the price that turns up in reception's box.
 *
 * A key the API sends that is missing here falls back to the key itself rather than rendering
 * an empty cell, so a new expense added server-side is ugly for one commit instead of invisible.
 */
export const expenseLabels: Record<string, string> = {
  bed_day: 'Bed, per day',
  food: 'Meals, per day',
  medicine: 'Medicine during the stay',
  therapy: 'Therapy, per session',
  tests: 'Tests and scans, each',
  transport: 'Transport home',
  take_home_medicine: 'Take-home medicine',
};

/** One line of plain English per expense, so the administrator knows what they are pricing. */
export const expenseHints: Record<string, string> = {
  bed_day:
    'The only one the system fills in by itself, off the ward the patient is actually lying in.',
  food: 'Charged per day the patient was fed, which is not always per day they were here.',
  medicine: 'Priced off the pharmacy slip, so it is different every time.',
  therapy: 'Physiotherapy and the like, per session attended.',
  tests: 'X-rays, blood work, anything sent to a lab.',
  transport: 'Only when the hospital arranges it.',
  take_home_medicine: 'What the patient leaves with.',
};

export function expenseLabel(key: string): string {
  return expenseLabels[key] ?? key;
}

/**
 * Whether a zero in this cell means "free" or "we cannot guess".
 *
 * Medicine has no fixed price and never will — the desk reads it off a slip. Showing `LKR 0.00`
 * against it reads as "no charge", which is the opposite of true, so those cells say
 * "typed at the desk" instead.
 */
export function isUnpriceable(key: string): boolean {
  return key === 'medicine' || key === 'take_home_medicine';
}

// ---------------------------------------------------------------------------
// The discharge checklist
// ---------------------------------------------------------------------------

// The keys the server publishes on Discharge.checklist, in the order they get done.
//
// It was five until 2026-09-11. `medication_issued`, `follow_up_recorded` and
// `transport_arranged` came off: each recorded that something had been given to or arranged for
// the patient, which is what a line on the bill records, with a price against it. Two records
// of one fact is how they come to disagree, and the checklist copy was the one nobody could
// price. Transport and take-home medicine are charge templates now — see chargeTemplates below.
export const checklistOrder = ['clinical_clearance', 'billing_settled'] as const;

/**
 * Which boxes hold up a discharge, mirroring DischargeService.Mandatory.
 *
 * Both of them, which makes the set look pointless. It is used before the server has written
 * any rows — the checklist is created the first time somebody ticks something, so a patient who
 * has just arrived has boxes to show and no data behind them. Once a row exists its own
 * `mandatory` is what the screen reads.
 */
export const mandatoryChecklistItems = new Set<string>([
  'clinical_clearance',
  'billing_settled',
]);

export const checklistLabels: Record<string, string> = {
  clinical_clearance: 'Cleared by a doctor',
  billing_settled: 'Bill settled',
};

/** Who does it, and anything the reader needs to know before pressing the button. */
export const checklistHints: Record<string, string> = {
  clinical_clearance:
    'A doctor only, and never anything automatic. Without this nobody goes home.',
  billing_settled:
    'Ticked by settling the bill below, and by nothing else. There is no button here on purpose: the bill and this box are the same fact.',
};

/** Falls back to the wire value, so a third box added server-side still reads sensibly. */
export function checklistLabel(item: string): string {
  return checklistLabels[item] ?? item.replaceAll('_', ' ');
}

// ---------------------------------------------------------------------------
// What reception can put on a bill
// ---------------------------------------------------------------------------

/**
 * The things a visit is commonly charged for, as a form rather than a blank box.
 *
 * The bill works out two lines on its own — the care level and the bed — and nothing else,
 * because no table in this project ties a treatment, a scan, a meal or a drug to an admission.
 * Everything else is typed at the desk. Typed into an empty "what for" box it came out as
 * "xray", "X-Ray" and "chest x ray" on three different bills; these give the same thing the
 * same name and the same price every time, and "Something else" is still there for the rest.
 *
 * **The prices are suggestions and they are invented.** No real price list was given to us.
 * They fill the box so the desk is not guessing, and every one of them stays editable.
 */
export type ChargeTemplate = {
  key: string;
  /** Goes onto the bill as the line description, so it is what the patient reads. */
  label: string;
  hint: string;
  /** Suggested and editable. Null where the amount genuinely varies every time. */
  unitPrice: number | null;
  /** What the quantity counts, because "How many" alone answers nothing. */
  quantityLabel: string;
  defaultQuantity: number;
};

export const chargeTemplates: ChargeTemplate[] = [
  {
    key: 'food',
    label: 'Meals',
    hint: 'Food for the stay. One per day the patient was fed, which is not always one per day they were here.',
    unitPrice: 1200,
    quantityLabel: 'Days',
    defaultQuantity: 1,
  },
  {
    key: 'medicine',
    label: 'Medicine during the stay',
    hint: 'What was given on the ward. Priced from the pharmacy slip — the amount varies every time, so there is no suggestion to make.',
    unitPrice: null,
    quantityLabel: 'Items',
    defaultQuantity: 1,
  },
  {
    key: 'therapy',
    label: 'Therapy',
    hint: 'Physiotherapy and the like, per session attended.',
    unitPrice: 4500,
    quantityLabel: 'Sessions',
    defaultQuantity: 1,
  },
  {
    key: 'tests',
    label: 'Tests and scans',
    hint: 'X-rays, blood work, anything sent to a lab. Name the test in the description.',
    unitPrice: 3500,
    quantityLabel: 'Tests',
    defaultQuantity: 1,
  },
  {
    key: 'transport',
    label: 'Transport home',
    hint: 'Only when the hospital arranges it. A patient whose family collects them is not charged for it, so this is not on every bill.',
    unitPrice: 3500,
    quantityLabel: 'Trips',
    defaultQuantity: 1,
  },
  {
    key: 'take_home_medicine',
    label: 'Take-home medicine',
    hint: 'What the patient leaves with. Charging for it and handing it over used to be two separate records; now the line on the bill is the one record.',
    unitPrice: null,
    quantityLabel: 'Items',
    defaultQuantity: 1,
  },
  {
    key: 'other',
    label: 'Something else',
    hint: 'Type whatever it was. Anything that turns up on three bills running probably wants a row of its own here.',
    unitPrice: null,
    quantityLabel: 'How many',
    defaultQuantity: 1,
  },
];

export function chargeTemplate(key: string): ChargeTemplate {
  return chargeTemplates.find((template) => template.key === key) ?? chargeTemplates[0];
}
