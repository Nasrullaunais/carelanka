import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  addAppointmentBillChargeMutation,
  addBillChargeMutation,
  getAdmissionBillOptions,
  getAppointmentBillOptions,
  prepareAdmissionBillMutation,
  prepareAppointmentBillMutation,
  removeAppointmentBillChargeMutation,
  removeBillChargeMutation,
  settleAppointmentBillMutation,
  settleBillMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type {
  AddBillChargeRequest,
  Bill,
  SettleBillRequest,
} from '../services/api/generated';
import {
  billLineSourceHints,
  billLineSourceLabels,
  chargeTemplate,
  chargeTemplates,
  isRemovableLine,
  money,
  quantity,
} from '../types/billing';

/// A bill belongs to an admission or to an appointment, never both, so the
/// panel takes exactly one of them and picks the matching endpoints. The two
/// differ only in what the generated half of the bill prices.
export type BillOwner =
  | { admissionId: string; appointmentId?: never }
  | { appointmentId: string; admissionId?: never };

export function BillPanel({
  canSettle,
  showPrint = true,
  ...owner
}: BillOwner & {
  canSettle: boolean;
  showPrint?: boolean;
}) {
  const queryClient = useQueryClient();

  const forAppointment = owner.appointmentId !== undefined;

  const visitWord = forAppointment ? 'appointment' : 'admission';

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

  // Both queries are declared because hooks cannot be called conditionally.
  // The idle one is disabled, so its placeholder path is never requested.
  const admissionBill = useQuery({
    ...getAdmissionBillOptions({ path: { admissionId: owner.admissionId ?? '' } }),
    enabled: !forAppointment,
    retry: false,
  });

  const appointmentBill = useQuery({
    ...getAppointmentBillOptions({ path: { appointmentId: owner.appointmentId ?? '' } }),
    enabled: forAppointment,
    retry: false,
  });

  const bill = forAppointment ? appointmentBill : admissionBill;

  const refreshAll = () =>
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

        return (
          id === 'getAdmissionBill' ||
          id === 'getAppointmentBill' ||
          id === 'listAppointments' ||
          id === 'listOutstandingBills' ||
          id === 'getAdmission' ||
          id === 'listAdmissions' ||
          id === 'listDischargeCandidates'
        );
      },
    });

  const onPrepared = (result: Bill) => {
    toast.success(`Bill ${result.bill_number} prepared.`);
    void refreshAll();
  };

  const onCharged = () => {
    toast.success('Charge added.');
    chooseTemplate(templateKey);
    void refreshAll();
  };

  const onChargeRemoved = () => {
    toast.success('Charge removed.');
    void refreshAll();
  };

  const onSettled = (result: Bill) => {
    toast.success(
      forAppointment
        ? `Bill ${result.bill_number} settled.`
        : `Bill ${result.bill_number} settled. Bill settled is now ticked.`,
    );
    void refreshAll();
  };

  // Both members of each pair are declared because hooks cannot be called
  // conditionally. Only the one matching this panel's owner is ever fired.
  const prepareAdmission = useMutation({
    ...prepareAdmissionBillMutation(),
    onSuccess: onPrepared,
  });
  const prepareAppointment = useMutation({
    ...prepareAppointmentBillMutation(),
    onSuccess: onPrepared,
  });

  const chargeAdmission = useMutation({ ...addBillChargeMutation(), onSuccess: onCharged });
  const chargeAppointment = useMutation({
    ...addAppointmentBillChargeMutation(),
    onSuccess: onCharged,
  });

  const unchargeAdmission = useMutation({
    ...removeBillChargeMutation(),
    onSuccess: onChargeRemoved,
  });
  const unchargeAppointment = useMutation({
    ...removeAppointmentBillChargeMutation(),
    onSuccess: onChargeRemoved,
  });

  const settleAdmission = useMutation({ ...settleBillMutation(), onSuccess: onSettled });
  const settleAppointment = useMutation({
    ...settleAppointmentBillMutation(),
    onSuccess: onSettled,
  });

  const prepare = forAppointment ? prepareAppointment : prepareAdmission;
  const addCharge = forAppointment ? chargeAppointment : chargeAdmission;
  const removeCharge = forAppointment ? unchargeAppointment : unchargeAdmission;
  const settle = forAppointment ? settleAppointment : settleAdmission;

  const runPrepare = () =>
    forAppointment
      ? prepareAppointment.mutate({ path: { appointmentId: owner.appointmentId! } })
      : prepareAdmission.mutate({ path: { admissionId: owner.admissionId! } });

  const runAddCharge = (body: AddBillChargeRequest) =>
    forAppointment
      ? chargeAppointment.mutate({ path: { appointmentId: owner.appointmentId! }, body })
      : chargeAdmission.mutate({ path: { admissionId: owner.admissionId! }, body });

  const runRemoveCharge = (lineId: string) =>
    forAppointment
      ? unchargeAppointment.mutate({
          path: { appointmentId: owner.appointmentId!, lineId },
        })
      : unchargeAdmission.mutate({ path: { admissionId: owner.admissionId!, lineId } });

  const runSettle = (body: SettleBillRequest) =>
    forAppointment
      ? settleAppointment.mutate({ path: { appointmentId: owner.appointmentId! }, body })
      : settleAdmission.mutate({ path: { admissionId: owner.admissionId! }, body });

  if (bill.isLoading) {
    return <p className="empty">Loading the bill…</p>;
  }

  if (!bill.data) {
    return (
      <>
        <p className="empty">
          No bill has been raised for this {visitWord} yet.
          {canSettle && (
            <>
              <br />
              <button
                type="button"
                style={{ marginTop: '0.9rem' }}
                disabled={prepare.isPending}
                onClick={() => runPrepare()}
              >
                {prepare.isPending ? 'Preparing…' : 'Raise the bill'}
              </button>
            </>
          )}
        </p>
        <p className="hint">
          {canSettle
            ? forAppointment
              ? "This adds the consultation fee at today's rate. Nobody was admitted, so there is no admission fee and no bed charge."
              : "This reads the admission — the care level and every bed used — and adds those lines at today's rates."
            : forAppointment
              ? 'Reception or the ward nurse raises the bill and takes payment.'
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

              runAddCharge({
                description: description.trim(),
                quantity: Number(chargeQuantity),
                unit_price: Number(unitPrice),
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
            {forAppointment ? (
              <>
                Treatments, tests and medicines are entered by hand — nothing in the system
                records them against a booking. The consultation fee is worked out
                automatically and is already listed above.
              </>
            ) : (
              <>
                Treatments, meals, tests and medicines are entered by hand — nothing in the
                system records them against an admission. The admission fee and bed charges are
                worked out automatically and are already listed above.
              </>
            )}
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
                  runSettle({ settlement_note: settlementNote.trim() || null })
                }
              >
                {settle.isPending ? 'Settling…' : `Settle ${money(data.total, data.currency)}`}
              </button>
            </div>
          </div>

          <p className="hint">
            {forAppointment ? (
              <>
                Settling makes the bill final. There is no discharge checklist to tick, because
                the patient was never admitted.
              </>
            ) : (
              <>
                Settling makes the bill final and ticks <strong>Bill settled</strong> on the
                discharge checklist. That item cannot be ticked any other way, so the payment
                and the checklist can never disagree.
              </>
            )}
          </p>

          <div className="actions" style={{ marginTop: '0.9rem' }}>
            <button
              type="button"
              className="secondary"
              disabled={prepare.isPending}
              onClick={() => runPrepare()}
            >
              {prepare.isPending
                ? 'Recalculating…'
                : forAppointment
                  ? 'Recalculate consultation fee'
                  : 'Recalculate bed days'}
            </button>
            {data.lines
              .filter((line) => isRemovableLine(line.source))
              .map((line) => (
                <button
                  key={line.id}
                  type="button"
                  className="secondary small danger"
                  disabled={removeCharge.isPending}
                  onClick={() => runRemoveCharge(line.id)}
                >
                  Remove &ldquo;{line.description}&rdquo;
                </button>
              ))}
          </div>

          <p className="hint">
            {forAppointment ? (
              <>
                Recalculating replaces the consultation fee with today&apos;s rate and leaves
                entered charges alone. Only an entered charge can be removed — the consultation
                fee would come straight back.
              </>
            ) : (
              <>
                Recalculating replaces the admission fee and bed lines with today&apos;s rates
                and leaves entered charges alone. Only an entered charge can be removed — a bed
                line would come straight back.
              </>
            )}
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
