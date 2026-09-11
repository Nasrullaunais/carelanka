import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  addBillChargeMutation,
  getAdmissionBillOptions,
  listOutstandingBillsOptions,
  prepareAdmissionBillMutation,
  removeBillChargeMutation,
  settleBillMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { Bill, OutstandingBill } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canWorkBillingDesk } from '../types/permissions';
import { admissionCategoryLabels } from '../types/patients';
import {
  billLineSourceHints,
  billLineSourceLabels,
  isRemovableLine,
  money,
  quantity,
} from '../types/billing';

// Reception's counter. Who owes money, what the bill comes to, and a printed copy to hand
// across.
//
// What a bill is made of, because somebody will be asked at the counter: an admission fee from
// the care level a clinician recorded, and a line per bed at that ward's day rate, worked out
// from the bed assignment rows. Nothing else is generated, because nothing else is stored —
// no table in this component records a treatment, a scan or a drug against a visit. So a
// charge for an X-ray is typed here, by a person, on purpose.
//
// Settling is the only thing in the whole system that ticks `billing_settled` on the discharge
// checklist. The checklist endpoint refuses that key outright, so a paid bill and an unticked
// box cannot happen.

export function BillingPage() {
  const session = useSession();
  const canWork = canWorkBillingDesk(session?.principal.role);

  const [search, setSearch] = useState('');
  const [applied, setApplied] = useState('');
  const [selected, setSelected] = useState<OutstandingBill | null>(null);

  const outstanding = useQuery({
    ...listOutstandingBillsOptions({
      query: { ...(applied ? { search: applied } : {}), pageSize: 50 },
    }),
    enabled: canWork,
  });

  if (!canWork) {
    return (
      <>
        <h1>Billing</h1>
        <p className="empty">
          Bills are settled at reception. General staff, the duty manager and the hospital
          administrator can open this screen.
        </p>
      </>
    );
  }

  const rows = outstanding.data?.items ?? [];
  const owed = rows.reduce((sum, row) => sum + row.estimated_total, 0);

  return (
    <>
      <h1>Billing</h1>
      <p className="muted">
        Visits in the building whose money has not been taken yet. Most have no bill written
        until you open one — a stay cannot be priced before it happens.
      </p>

      <div className="card">
        <h2>Unpaid visits</h2>

        <form
          className="row"
          onSubmit={(event: FormEvent) => {
            event.preventDefault();
            setApplied(search.trim());
          }}
        >
          <div>
            <label htmlFor="billing-search">Find a patient</label>
            <input
              id="billing-search"
              value={search}
              placeholder="Name, patient code or NIC"
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
          <div style={{ alignSelf: 'end' }}>
            <button type="submit" className="secondary">
              Search
            </button>
          </div>
        </form>

        <div className="stats">
          <Stat caption="Unpaid visits" value={String(rows.length)} />
          <Stat caption="Outstanding" value={money(owed)} />
        </div>

        {outstanding.isError ? (
          <div className="empty">
            <p>Could not load the list.</p>
            <button type="button" className="secondary" onClick={() => void outstanding.refetch()}>
              Try again
            </button>
          </div>
        ) : outstanding.isLoading ? (
          <p className="empty">Loading…</p>
        ) : rows.length === 0 ? (
          <p className="empty">Nothing outstanding.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Patient</th>
                <th>Where</th>
                <th>Care level</th>
                <th>Bill</th>
                <th>Comes to</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr
                  key={row.admission_id}
                  className={row.admission_id === selected?.admission_id ? 'selected' : undefined}
                >
                  <td>
                    <strong>{row.patient.full_name}</strong>
                    <div className="small muted">{row.patient.patient_code}</div>
                  </td>
                  <td>
                    {row.bed_number ? (
                      <>
                        {row.ward_name}
                        <div className="small muted">Bed {row.bed_number}</div>
                      </>
                    ) : (
                      <span className="muted">No bed</span>
                    )}
                  </td>
                  <td>{admissionCategoryLabels[row.admission_category]}</td>
                  <td>
                    {row.bill_number ?? <span className="muted">Not opened</span>}
                  </td>
                  <td>{money(row.estimated_total, row.currency)}</td>
                  <td>
                    <button
                      type="button"
                      className="secondary small"
                      onClick={() =>
                        setSelected(
                          row.admission_id === selected?.admission_id ? null : row,
                        )
                      }
                    >
                      {row.admission_id === selected?.admission_id ? 'Close' : 'Open bill'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <p className="hint">
          &ldquo;Comes to&rdquo; is what the bill would total if you opened it now — the stay is
          still running, so it moves. Opening the bill is what writes the lines down.
        </p>
      </div>

      {selected && (
        <BillPanel
          admissionId={selected.admission_id}
          patientName={selected.patient.full_name}
          onClose={() => setSelected(null)}
        />
      )}
    </>
  );
}

// ---------------------------------------------------------------------------
// One bill
// ---------------------------------------------------------------------------

function BillPanel({
  admissionId,
  patientName,
  onClose,
}: {
  admissionId: string;
  patientName: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();

  const [description, setDescription] = useState('');
  const [chargeQuantity, setChargeQuantity] = useState('1');
  const [unitPrice, setUnitPrice] = useState('');
  const [settlementNote, setSettlementNote] = useState('');

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
          id === 'listDischargeCandidates'
        );
      },
    });

  const prepare = useMutation({
    ...prepareAdmissionBillMutation(),
    onSuccess: (result) => {
      toast.success(`Bill ${result.bill_number} worked out.`);
      void refreshAll();
    },
  });

  const addCharge = useMutation({
    ...addBillChargeMutation(),
    onSuccess: () => {
      toast.success('Charge added.');
      setDescription('');
      setChargeQuantity('1');
      setUnitPrice('');
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
      toast.success(`Bill ${result.bill_number} settled. The checklist box is ticked.`);
      void refreshAll();
    },
  });

  if (bill.isLoading) {
    return (
      <div className="card">
        <p className="empty">Loading…</p>
      </div>
    );
  }

  // No bill row yet. Not an error state — say what is missing and offer the one button that
  // fixes it.
  if (!bill.data) {
    return (
      <div className="card">
        <div className="dialog-head">
          <h2>{patientName}</h2>
          <button type="button" className="secondary small" onClick={onClose}>
            Close
          </button>
        </div>
        <p className="empty">
          No bill has been opened for this visit yet.
          <br />
          <button
            type="button"
            style={{ marginTop: '0.9rem' }}
            disabled={prepare.isPending}
            onClick={() => prepare.mutate({ path: { admissionId } })}
          >
            {prepare.isPending ? 'Working it out…' : 'Open the bill'}
          </button>
        </p>
        <p className="hint">
          That reads the visit — the care level and every bed the patient has been in — and
          writes the lines down at today's rates.
        </p>
      </div>
    );
  }

  const data = bill.data;
  const frozen = data.settled;

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>
          Bill {data.bill_number}{' '}
          {frozen ? (
            <span className="badge status-available">Settled</span>
          ) : (
            <span className="badge severity-high">Not settled</span>
          )}
        </h2>
        <div className="actions">
          <button type="button" className="secondary small" onClick={() => window.print()}>
            Print / save
          </button>
          <button type="button" className="secondary small" onClick={onClose}>
            Close
          </button>
        </div>
      </div>

      {/* The printable half. Everything outside it is hidden by the print stylesheet, so what
          comes out of the printer is a bill and not a screenshot of an app. */}
      <BillPrintout bill={data} />

      {frozen ? (
        <p className="hint">
          Settled{data.settled_at && ` at ${new Date(data.settled_at).toLocaleString()}`}
          {data.settlement_note && ` — ${data.settlement_note}`}. A settled bill is frozen: it is
          the piece of paper the patient was handed, so nothing may be added to it afterwards.
        </p>
      ) : (
        <>
          <h3>Add a charge</h3>

          <form
            className="row"
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
            <div>
              <label htmlFor="charge-description">What for</label>
              <input
                id="charge-description"
                required
                maxLength={200}
                value={description}
                placeholder="Chest X-ray"
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
            <div>
              <label htmlFor="charge-quantity">How many</label>
              <input
                id="charge-quantity"
                required
                type="number"
                min="0.01"
                step="0.01"
                value={chargeQuantity}
                onChange={(event) => setChargeQuantity(event.target.value)}
              />
            </div>
            <div>
              <label htmlFor="charge-price">Each (LKR)</label>
              <input
                id="charge-price"
                required
                type="number"
                min="0"
                step="0.01"
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
          </form>

          <p className="hint">
            Treatments, scans and medicines are typed here because nothing in the system records
            them against a visit. Bed time and care level are the only things it can price on its
            own, and it does — those lines are already above.
          </p>

          <h3>Settle</h3>

          <div className="row">
            <div>
              <label htmlFor="settlement-note">
                How it was paid <span className="muted">(optional)</span>
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
                {settle.isPending ? 'Settling…' : `Take ${money(data.total, data.currency)}`}
              </button>
            </div>
          </div>

          <p className="hint">
            Settling freezes the bill and ticks <strong>Bill settled</strong> on the discharge
            checklist, in one go. That box cannot be ticked any other way, so the money and the
            ward's checklist can never disagree.
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
                  onClick={() =>
                    removeCharge.mutate({ path: { admissionId, lineId: line.id } })
                  }
                >
                  Remove &ldquo;{line.description}&rdquo;
                </button>
              ))}
          </div>

          <p className="hint">
            Recalculating replaces the fee and bed lines with today's numbers and leaves typed
            charges alone. Only a typed charge can be removed — a bed line would come straight
            back.
          </p>
        </>
      )}
    </div>
  );
}

/**
 * The bill itself, and the only part of the page that reaches a printer.
 *
 * A browser print view rather than a generated PDF: it is one stylesheet against a stack that
 * already renders the numbers, it saves to PDF from the print dialog anyway, and it does not
 * put a document-generation dependency into a project that needs one screen of it.
 */
function BillPrintout({ bill }: { bill: Bill }) {
  return (
    <div className="printable">
      <div className="print-only print-head">
        <h2>CareLanka Hospital</h2>
        <p>Statement of charges</p>
      </div>

      <dl className="detail-grid">
        <dt>Patient</dt>
        <dd>
          {bill.patient.full_name}
          <div className="small muted">
            {bill.patient.patient_code}
            {bill.patient.nic && ` · ${bill.patient.nic}`}
          </div>
        </dd>
        <dt>Bill number</dt>
        <dd>{bill.bill_number}</dd>
        <dt>Raised</dt>
        <dd>{new Date(bill.created_at).toLocaleString()}</dd>
      </dl>

      <table>
        <thead>
          <tr>
            <th>What for</th>
            <th>How many</th>
            <th>Each</th>
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

function Stat({ caption, value }: { caption: string; value: string }) {
  return (
    <div className="stat">
      <div className="value">{value}</div>
      <div className="caption">{caption}</div>
    </div>
  );
}
