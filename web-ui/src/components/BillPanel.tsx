import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  addBillChargeMutation,
  getAdmissionBillOptions,
  prepareAdmissionBillMutation,
  removeBillChargeMutation,
  settleBillMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { Bill } from '../services/api/generated';
import {
  billLineSourceHints,
  billLineSourceLabels,
  chargeTemplate,
  chargeTemplates,
  isRemovableLine,
  money,
  quantity,
} from '../types/billing';

// One bill, everywhere a bill is shown.
//
// It lives in components/ rather than in a page because it started out on a billing screen of
// its own. That screen is gone (2026-09-12): "where does the billing happen?" was a fair
// question to ask of a discharge checklist that had a `Bill settled` box, no bill on the page
// and no way to reach one — and the answer, a second screen, meant walking the same visit
// twice while neither page could see what the other had done. There is one screen now and this
// is the part of it that is the bill.
//
// Two things it deliberately does NOT do:
//
//   It does not decide who may settle. `canSettle` is passed in, because the billing desk and
//   the discharge checklist are different permissions and only the duty manager holds both.
//   A ward nurse sees the bill and reads the total; they do not take money.
//
//   It does not invent a charge. The fee and the bed are worked out from the visit; everything
//   else is typed by a person, because no table in this project ties a treatment, a meal or a
//   drug to an admission. A fake charge is a number handed to a patient on paper.

export function BillPanel({
  admissionId,
  canSettle,
  showPrint = true,
}: {
  admissionId: string;
  /** Whether this viewer may add charges and take money — Policies.BillingDesk. */
  canSettle: boolean;
  showPrint?: boolean;
}) {
  const queryClient = useQueryClient();

  const [templateKey, setTemplateKey] = useState(chargeTemplates[0].key);
  const [description, setDescription] = useState(chargeTemplates[0].label);
  const [chargeQuantity, setChargeQuantity] = useState('1');
  const [unitPrice, setUnitPrice] = useState(String(chargeTemplates[0].unitPrice ?? ''));
  const [settlementNote, setSettlementNote] = useState('');

  const template = chargeTemplate(templateKey);

  /** Picking a row refills the three boxes under it; the typist can still overwrite any of them. */
  function chooseTemplate(key: string) {
    const chosen = chargeTemplate(key);

    setTemplateKey(key);
    setDescription(chosen.key === 'other' ? '' : chosen.label);
    setChargeQuantity(String(chosen.defaultQuantity));
    setUnitPrice(chosen.unitPrice === null ? '' : String(chosen.unitPrice));
  }

  const bill = useQuery({
    ...getAdmissionBillOptions({ path: { admissionId } }),

    // A 404 here means "nobody has opened a bill yet", which is the ordinary state and not a
    // failure. Retrying would just ask again and toast again.
    retry: false,
  });

  // Settling moves the discharge checklist and can move the admission's status, so the ward
  // screens have to hear about it too.
  const refreshAll = () =>
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

        return (
          id === 'getAdmissionBill' ||
          id === 'listOutstandingBills' ||
          id === 'getAdmission' ||
          id === 'listAdmissions' ||
          id === 'listDischargeCandidates'
        );
      },
    });

  const prepare = useMutation({
    ...prepareAdmissionBillMutation(),
    onSuccess: (result) => {
      toast.success(`Bill ${result.bill_number} prepared.`);
      void refreshAll();
    },
  });

  const addCharge = useMutation({
    ...addBillChargeMutation(),
    onSuccess: () => {
      toast.success('Charge added.');
      chooseTemplate(templateKey);
      void refreshAll();
    },
  });

  const removeCharge = useMutation({
    ...removeBillChargeMutation(),
    onSuccess: () => {
      toast.success('Charge removed.');
      void refreshAll();
    },
  });

  const settle = useMutation({
    ...settleBillMutation(),
    onSuccess: (result) => {
      toast.success(`Bill ${result.bill_number} settled. Bill settled is now ticked.`);
      void refreshAll();
    },
  });

  if (bill.isLoading) {
    return <p className="empty">Loading the bill…</p>;
  }

  // No bill row yet. Not an error state — say what is missing and offer the one button that
  // fixes it, to whoever is allowed to press it.
  if (!bill.data) {
    return (
      <>
        <p className="empty">
          No bill has been raised for this admission yet.
          {canSettle && (
            <>
              <br />
              <button
                type="button"
                style={{ marginTop: '0.9rem' }}
                disabled={prepare.isPending}
                onClick={() => prepare.mutate({ path: { admissionId } })}
              >
                {prepare.isPending ? 'Preparing…' : 'Raise the bill'}
              </button>
            </>
          )}
        </p>
        <p className="hint">
          {canSettle
            ? "This reads the admission — the care level and every bed used — and adds those lines at today's rates."
            : 'Reception raises the bill and takes payment. Nothing on the ward is held up until the discharge itself.'}
        </p>
      </>
    );
  }

  const data = bill.data;
  const frozen = data.settled;

  return (
    <>
      {showPrint && (
        <div className="actions" style={{ justifyContent: 'flex-end' }}>
          <button type="button" className="secondary small" onClick={() => window.print()}>
            Print / save
          </button>
        </div>
      )}

      {/* The printable half. Everything outside it is hidden by the print stylesheet, so what
          comes out of the printer is a bill and not a screenshot of an app. */}
      <BillPrintout bill={data} standalone={showPrint} />

      {/* Everything below is a control, not the document. The discharge card prints as a
          whole, so the charge form and the settle form have to say so themselves - otherwise
          the patient's copy of their bill comes out with an empty "Add a charge" form on it. */}
      <div className="no-print">
      {frozen ? (
        <p className="hint">
          Settled{data.settled_at && ` at ${new Date(data.settled_at).toLocaleString()}`}
          {data.settled_by_staff_name && ` by ${data.settled_by_staff_name}`}
          {data.settlement_note && ` — ${data.settlement_note}`}. A settled bill is final and
          cannot be changed.
        </p>
      ) : !canSettle ? (
        <p className="hint">
          <strong>{money(data.total, data.currency)} outstanding.</strong> Payment is taken by
          reception. This is shown so the ward can see how far the discharge has got.
        </p>
      ) : (
        <>
          <h3>Add a charge</h3>

          <form
            onSubmit={(event: FormEvent) => {
              event.preventDefault();

              addCharge.mutate({
                path: { admissionId },
                body: {
                  description: description.trim(),
                  quantity: Number(chargeQuantity),
                  unit_price: Number(unitPrice),
                },
              });
            }}
          >
            <div className="row">
              <div className="field">
                <label htmlFor="charge-kind">Charge type</label>
                <select
                  id="charge-kind"
                  value={templateKey}
                  onChange={(event) => chooseTemplate(event.target.value)}
                >
                  {chargeTemplates.map((option) => (
                    <option key={option.key} value={option.key}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="charge-description">Description on the bill</label>
                <input
                  id="charge-description"
                  required
                  maxLength={200}
                  value={description}
                  placeholder="Chest X-ray"
                  onChange={(event) => setDescription(event.target.value)}
                />
              </div>
              <div className="field">
                <label htmlFor="charge-quantity">{template.quantityLabel}</label>
                <input
                  id="charge-quantity"
                  required
                  type="number"
                  min="1"
                  step="1"
                  value={chargeQuantity}
                  onChange={(event) => setChargeQuantity(event.target.value)}
                />
              </div>
              <div className="field">
                <label htmlFor="charge-price">Unit price (LKR)</label>
                <input
                  id="charge-price"
                  required
                  type="number"
                  min="0"
                  step="any"
                  value={unitPrice}
                  placeholder="3500"
                  onChange={(event) => setUnitPrice(event.target.value)}
                />
              </div>
              <div style={{ alignSelf: 'end' }}>
                <button type="submit" disabled={addCharge.isPending}>
                  {addCharge.isPending ? 'Adding…' : 'Add'}
                </button>
              </div>
            </div>
          </form>

          <p className="hint">
            {template.hint}
            {template.unitPrice !== null && (
              <>
                {' '}
                The price shown is a default. Change it to the amount actually charged.
              </>
            )}
          </p>

          <p className="hint">
            Treatments, meals, tests and medicines are entered by hand — nothing in the system
            records them against an admission. The admission fee and bed charges are worked out
            automatically and are already listed above.
          </p>

          <h3>Settle</h3>

          <div className="row">
            <div className="field">
              <label htmlFor="settlement-note">
                Payment method <span className="muted">(optional)</span>
              </label>
              <input
                id="settlement-note"
                maxLength={300}
                value={settlementNote}
                placeholder="Cash"
                onChange={(event) => setSettlementNote(event.target.value)}
              />
            </div>
            <div style={{ alignSelf: 'end' }}>
              <button
                type="button"
                disabled={settle.isPending}
                onClick={() =>
                  settle.mutate({
                    path: { admissionId },
                    body: { settlement_note: settlementNote.trim() || null },
                  })
                }
              >
                {settle.isPending ? 'Settling…' : `Settle ${money(data.total, data.currency)}`}
              </button>
            </div>
          </div>

          <p className="hint">
            Settling makes the bill final and ticks <strong>Bill settled</strong> on the
            discharge checklist. That item cannot be ticked any other way, so the payment and
            the checklist can never disagree.
          </p>

          <div className="actions" style={{ marginTop: '0.9rem' }}>
            <button
              type="button"
              className="secondary"
              disabled={prepare.isPending}
              onClick={() => prepare.mutate({ path: { admissionId } })}
            >
              {prepare.isPending ? 'Recalculating…' : 'Recalculate bed days'}
            </button>
            {data.lines
              .filter((line) => isRemovableLine(line.source))
              .map((line) => (
                <button
                  key={line.id}
                  type="button"
                  className="secondary small danger"
                  disabled={removeCharge.isPending}
                  onClick={() => removeCharge.mutate({ path: { admissionId, lineId: line.id } })}
                >
                  Remove &ldquo;{line.description}&rdquo;
                </button>
              ))}
          </div>

          <p className="hint">
            Recalculating replaces the admission fee and bed lines with today's rates and leaves
            entered charges alone. Only an entered charge can be removed — a bed line would come
            straight back.
          </p>
        </>
      )}
      </div>
    </>
  );
}

/**
 * The bill itself, and the only part of the page that reaches a printer.
 *
 * A browser print view rather than a generated PDF: it is one stylesheet against a stack that
 * already renders the numbers, it saves to PDF from the print dialog anyway, and it does not
 * put a document-generation dependency into a project that needs one screen of it.
 */
export function BillPrintout({
  bill,
  standalone = true,
}: {
  bill: Bill;
  /**
   * Whether this is the whole printed document or a section of a bigger one.
   *
   * On the discharge screen it is a section: the card around it owns the hospital heading, the
   * patient block and the `printable` class, and repeating all three inside would print the
   * patient's name twice under two different headings.
   */
  standalone?: boolean;
}) {
  return (
    <div className={standalone ? 'printable' : undefined}>
      {standalone && (
        <div className="print-only print-head">
          <h2>CareLanka Hospital</h2>
          <p>Statement of charges</p>
        </div>
      )}

      {/* Each label and value wrapped, so the grid cannot split a pair across rows and leave
          every label sitting above somebody else's value. */}
      <dl className="detail-grid">
        {standalone && (
          <div>
            <dt>Patient</dt>
            <dd>
              {bill.patient.full_name}
              <div className="small muted">
                {bill.patient.patient_code}
                {bill.patient.nic && ` · ${bill.patient.nic}`}
              </div>
            </dd>
          </div>
        )}
        <div>
          <dt>Bill number</dt>
          <dd>{bill.bill_number}</dd>
        </div>
        <div>
          <dt>Raised on</dt>
          <dd>
            {new Date(bill.created_at).toLocaleString()}
            {/* Who issued it. A bill handed across a counter names the person who wrote it,
                and "who do I ask about this charge?" is the first question at the desk. */}
            {bill.raised_by_staff_name && (
              <div className="small muted">by {bill.raised_by_staff_name}</div>
            )}
          </dd>
        </div>
        <div>
          <dt>Settled</dt>
          <dd>
            {bill.settled ? (
              <>
                {bill.settled_by_staff_name ?? 'Paid'}
                <div className="small muted">
                  {bill.settled_at && new Date(bill.settled_at).toLocaleString()}
                  {bill.settlement_note && ` · ${bill.settlement_note}`}
                </div>
              </>
            ) : (
              <span className="muted">Not yet</span>
            )}
          </dd>
        </div>
      </dl>

      <table>
        <thead>
          <tr>
            <th>Description</th>
            <th>Quantity</th>
            <th>Unit price</th>
            <th>Amount</th>
          </tr>
        </thead>
        <tbody>
          {bill.lines.map((line) => (
            <tr key={line.id}>
              <td>
                {line.description}
                <div className="small muted">{billLineSourceLabels[line.source]}</div>
              </td>
              <td>{quantity(line.quantity)}</td>
              <td>{money(line.unit_price, bill.currency)}</td>
              <td>{money(line.line_total, bill.currency)}</td>
            </tr>
          ))}
          <tr>
            <th scope="row" colSpan={3}>
              Total
            </th>
            <td>
              <strong>{money(bill.total, bill.currency)}</strong>
            </td>
          </tr>
        </tbody>
      </table>

      <p className="print-only print-foot">
        {bill.settled
          ? `Paid${bill.settlement_note ? ` — ${bill.settlement_note}` : ''}.`
          : 'Payment outstanding.'}
      </p>

      <p className="hint no-print">
        {[...new Set(bill.lines.map((line) => line.source))]
          .map((source) => `${billLineSourceLabels[source]}: ${billLineSourceHints[source]}`)
          .join(' ')}
      </p>
    </div>
  );
}
