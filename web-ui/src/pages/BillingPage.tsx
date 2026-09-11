import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listOutstandingBillsOptions } from '../services/api/generated/@tanstack/react-query.gen';
import type { OutstandingBill } from '../services/api/generated';
import { BillPanel } from '../components/BillPanel';
import { useSession } from '../services/auth/useSession';
import { canWorkBillingDesk } from '../types/permissions';
import { admissionCategoryLabels } from '../types/patients';
import { money } from '../types/billing';

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
  const [includeSettled, setIncludeSettled] = useState(false);
  const [selected, setSelected] = useState<OutstandingBill | null>(null);

  const outstanding = useQuery({
    ...listOutstandingBillsOptions({
      query: {
        ...(applied ? { search: applied } : {}),
        ...(includeSettled ? { includeSettled: true } : {}),
        pageSize: 50,
      },
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

  // Only the unpaid ones count towards what the hospital is owed. With the toggle on, the list
  // is a search result rather than a worklist, and adding paid bills into the total would make
  // the headline number jump for no reason anybody could explain.
  const owed = rows
    .filter((row) => !row.settled)
    .reduce((sum, row) => sum + row.estimated_total, 0);

  return (
    <>
      <h1>Billing</h1>
      <p className="muted">
        Visits in the building whose money has not been taken yet. Most have no bill written
        until you open one — a stay cannot be priced before it happens. Tick the box below to
        find a bill somebody has already paid.
      </p>

      <div className="card">
        {/* The heading follows the toggle. With paid bills in the list it is a search result,
            not a worklist, and calling it "unpaid" while a row says "Paid" is the kind of small
            contradiction that makes people distrust the whole screen. */}
        <h2>{includeSettled ? 'Find a bill' : 'Unpaid visits'}</h2>

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

        <div className="field">
          <label htmlFor="include-settled">
            <input
              id="include-settled"
              type="checkbox"
              checked={includeSettled}
              onChange={(event) => {
                setIncludeSettled(event.target.checked);
                setSelected(null);
              }}
            />{' '}
            Include bills already paid
          </label>
          <p className="hint">
            Off, this is the work still to do. On, it finds any visit that has a bill at all —
            including patients who have paid and gone home — so you can print someone a second
            copy when they ask at the counter.
          </p>
        </div>

        <div className="stats">
          <Stat
            caption={includeSettled ? 'Bills found' : 'Unpaid visits'}
            value={String(rows.length)}
          />
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
          <p className="empty">
            {includeSettled
              ? 'No bills match that search.'
              : 'Nothing outstanding.'}
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Patient</th>
                <th>Where</th>
                <th>Care level</th>
                <th>Bill</th>
                <th>Comes to</th>
                <th>Paid</th>
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
                    {row.settled ? (
                      <>
                        <span className="badge status-available">Paid</span>
                        {row.settled_at && (
                          <div className="small muted">
                            {new Date(row.settled_at).toLocaleDateString()}
                          </div>
                        )}
                      </>
                    ) : (
                      <span className="muted">No</span>
                    )}
                  </td>
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
          &ldquo;Comes to&rdquo; is what the bill would total if you opened it now — for a visit
          still running, that moves. Opening the bill is what writes the lines down; once it is
          paid the figure is fixed.
        </p>
      </div>

      {selected && (
        <div className="card">
          <div className="dialog-head">
            <h2>{selected.patient.full_name}</h2>
            <button type="button" className="secondary small" onClick={() => setSelected(null)}>
              Close
            </button>
          </div>

          {/* canSettle is true here by construction - the whole page is behind
              canWorkBillingDesk - but it is passed rather than assumed, because the same
              panel renders read-only on the discharge screen for a ward nurse. */}
          <BillPanel admissionId={selected.admission_id} canSettle />
        </div>
      )}
    </>
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
