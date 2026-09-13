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

export function BillPanel({
  admissionId,
  canSettle,
  showPrint = true,
}: {
  admissionId: string;

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

  function chooseTemplate(key: string) {
    const chosen = chargeTemplate(key);

    setTemplateKey(key);
    setDescription(chosen.key === 'other' ? '' : chosen.label);
    setChargeQuantity(String(chosen.defaultQuantity));
    setUnitPrice(chosen.unitPrice === null ? '' : String(chosen.unitPrice));
  }

  const bill = useQuery({
    ...getAdmissionBillOptions({ path: { admissionId } }),

    retry: false,
  });

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

      <BillPrintout bill={data} standalone={showPrint} />

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

export function BillPrintout({
  bill,
  standalone = true,
}: {
  bill: Bill;

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
