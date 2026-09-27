import type { BillLineSource } from '../services/api/generated';

export const billLineSourceLabels: Record<BillLineSource, string> = {
  admission_fee: 'Admission fee',
  bed_stay: 'Bed',
  consultation_fee: 'Consultation fee',
  manual: 'Added manually',
};

export const billLineSourceHints: Record<BillLineSource, string> = {
  admission_fee: 'Worked out from the care level recorded by a clinician.',
  bed_stay: 'Worked out from the time the patient spent in that bed.',
  consultation_fee: 'The standard charge for being seen at a booked appointment.',
  manual: 'Entered by hand. Nothing in the system records treatments automatically.',
};

export function isRemovableLine(source: BillLineSource): boolean {
  return source === 'manual';
}

export function money(amount: number, currency = 'LKR'): string {
  return `${currency} ${amount.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export function quantity(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}

export const expenseLabels: Record<string, string> = {
  bed_day: 'Bed, per day',
  food: 'Meals, per day',
  medicine: 'Medicine during the stay',
  therapy: 'Therapy, per session',
  tests: 'Tests and scans, each',
  transport: 'Transport home',
  take_home_medicine: 'Take-home medicine',
};

export const expenseHints: Record<string, string> = {
  bed_day: 'The only rate applied automatically, taken from the ward the patient is in.',
  food: 'Charged per day the patient was fed, which is not always per day admitted.',
  medicine: 'Priced from the pharmacy slip, so it varies every time.',
  therapy: 'Physiotherapy and similar, per session attended.',
  tests: 'X-rays, blood work and anything sent to a lab.',
  transport: 'Charged only when the hospital arranges it.',
  take_home_medicine: 'Medicine the patient leaves with.',
};

export function expenseLabel(key: string): string {
  return expenseLabels[key] ?? key;
}

export function isUnpriceable(key: string): boolean {
  return key === 'medicine' || key === 'take_home_medicine';
}

export const checklistOrder = ['clinical_clearance', 'billing_settled'] as const;

export const mandatoryChecklistItems = new Set<string>([
  'clinical_clearance',
  'billing_settled',
]);

export const checklistLabels: Record<string, string> = {
  clinical_clearance: 'Cleared by a doctor',
  billing_settled: 'Bill settled',
};

export const checklistHints: Record<string, string> = {
  clinical_clearance: 'Recorded by a doctor only. No discharge without it.',
  billing_settled:
    'Ticked by settling the bill above, and by nothing else. There is no button here on purpose.',
};

export function checklistLabel(item: string): string {
  return checklistLabels[item] ?? item.replaceAll('_', ' ');
}

export type ChargeTemplate = {
  key: string;

  label: string;
  hint: string;

  unitPrice: number | null;

  quantityLabel: string;
  defaultQuantity: number;
};

/// Meals, ward medicine and transport home only make sense once someone is actually
/// staying — offering them on an appointment bill is what let "Meals" sit as the default
/// charge type for a patient who was only ever seen at a desk and sent home.
const stayOnlyTemplates: ChargeTemplate[] = [
  {
    key: 'food',
    label: 'Meals',
    hint: 'One per day the patient was fed, which is not always one per day admitted.',
    unitPrice: 1200,
    quantityLabel: 'Days',
    defaultQuantity: 1,
  },
  {
    key: 'medicine',
    label: 'Medicine during the stay',
    hint: 'Medicine given on the ward, priced from the pharmacy slip. The amount varies every time, so no default is offered.',
    unitPrice: null,
    quantityLabel: 'Items',
    defaultQuantity: 1,
  },
  {
    key: 'transport',
    label: 'Transport home',
    hint: 'Charged only when the hospital arranges it. A patient collected by family is not charged.',
    unitPrice: 3500,
    quantityLabel: 'Trips',
    defaultQuantity: 1,
  },
];

/// Charges that make sense whether or not the patient was ever admitted.
const commonTemplates: ChargeTemplate[] = [
  {
    key: 'tests',
    label: 'Blood test, scan or X-ray',
    hint: 'Blood work, imaging and anything sent to a lab. Name the test in the description.',
    unitPrice: 3500,
    quantityLabel: 'Tests',
    defaultQuantity: 1,
  },
  {
    key: 'therapy',
    label: 'Therapy',
    hint: 'Physiotherapy and similar, per session attended.',
    unitPrice: 4500,
    quantityLabel: 'Sessions',
    defaultQuantity: 1,
  },
  {
    key: 'take_home_medicine',
    label: 'Medicine to take home',
    hint: 'Medicine the patient leaves with. The line on the bill is the only record of it.',
    unitPrice: null,
    quantityLabel: 'Items',
    defaultQuantity: 1,
  },
  {
    key: 'other',
    label: 'Other',
    hint: 'Anything not listed above. Describe it in full — the patient reads this line.',
    unitPrice: null,
    quantityLabel: 'How many',
    defaultQuantity: 1,
  },
];

export const admissionChargeTemplates: ChargeTemplate[] = [...commonTemplates, ...stayOnlyTemplates];
export const appointmentChargeTemplates: ChargeTemplate[] = commonTemplates;

export function chargeTemplatesFor(forAppointment: boolean): ChargeTemplate[] {
  return forAppointment ? appointmentChargeTemplates : admissionChargeTemplates;
}

export function chargeTemplate(key: string, forAppointment: boolean): ChargeTemplate {
  const templates = chargeTemplatesFor(forAppointment);
  return templates.find((template) => template.key === key) ?? templates[0];
}
