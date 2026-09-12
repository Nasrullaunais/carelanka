import { Fragment, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  confirmDischargeMutation,
  getAdmissionOptions,
  listDischargeCandidatesOptions,
  updateDischargeChecklistMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { ChecklistItem, DischargeCandidate } from '../services/api/generated';
import { BillPanel } from '../components/BillPanel';
import { useSession } from '../services/auth/useSession';
import {
  canConfirmDischarge,
  canTickChecklistItem,
  canWorkBillingDesk,
  canOpenDischargeBoard,
} from '../types/permissions';
import { admissionCategoryLabels, admissionStatusLabels } from '../types/patients';
import {
  checklistHints,
  checklistLabel,
  checklistOrder,
  mandatoryChecklistItems,
} from '../types/billing';

// Discharging a patient. Two tables — current admissions, and discharged ones — and one detail
// drawer that opens under the row it belongs to.
//
// Three things on this screen are worth knowing before reading the code:
//
//   The list is not the decision. A patient with every box ticked is only *offered*; a human
//   presses the button, and that is the whole point of the gate.
//
//   Only the boxes THIS role may tick render as buttons. A ward nurse never sees a control for
//   "Cleared by a doctor" — offering it and answering 403 is a worse screen than not offering
//   it, because the nurse cannot tell a permission from a bug.
//
//   "Bill settled" has no button for anybody. It is written by settling the bill — which now
//   happens on THIS page, in the middle of the flow — and nowhere else, so the money and the
//   tick are one fact rather than two that can drift apart.
//
// The order down the drawer is the order the job is done in: a doctor clears the patient, the
// bill is raised and paid, then somebody confirms the discharge. The drawer used to be a card
// at the foot of the page, which meant scrolling past everyone else to read about one person.
// It is a row of the table now, like the patients board.

export function DischargePage() {
  const session = useSession();
  const role = session?.principal.role;
  const canWork = canOpenDischargeBoard(role);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  // The hook cannot go behind an early return, so it is switched off instead. Nobody who
  // cannot work the checklist should be asking the server at all.
  // includeDischarged on purpose. Without it this screen empties itself the moment the work is
  // done: you confirm a discharge, the row vanishes, and there is nowhere left to look up what
  // just happened. The finished ones come back sorted below everybody still in the building.
  const candidates = useQuery({
    ...listDischargeCandidatesOptions({ query: { includeDischarged: true, pageSize: 50 } }),
    enabled: canWork,
  });

  if (!canWork) {
    return (
      <>
        <h1>Discharge</h1>
        <p className="empty">
          Your role cannot work discharges. Ward nurses, doctors, the duty manager, reception
          and the hospital administrator can.
        </p>
      </>
    );
  }

  const all = candidates.data?.items ?? [];

  // Two lists out of one call, because they are two different things to look at. The top one
  // is work to do; the bottom one is a record of work finished, and mixing them puts a patient
  // who left last Tuesday in among the ones a nurse is trying to discharge today.
  const rows = all.filter((row) => !row.is_discharged);
  const finished = all.filter((row) => row.is_discharged);
  const ready = rows.filter((row) => row.outstanding_items.length === 0);

  return (
    <>
      <h1>Discharge</h1>
      <p className="muted">
        Current admissions, what is outstanding on each, and the bill. Open a row to raise or
        settle the bill and confirm the discharge.
      </p>

      {candidates.isError ? (
        <div className="card">
          <div className="empty">
            <p>Could not load the discharge board.</p>
            <button type="button" className="secondary" onClick={() => void candidates.refetch()}>
              Try again
            </button>
          </div>
        </div>
      ) : (
        <div className="card">
          <h2>Current admissions</h2>

          <div className="stats">
            <Stat caption="Admitted" value={rows.length} />
            <Stat
              caption="Ready for discharge"
              value={ready.length}
              tone={ready.length === 0 ? 'none' : 'free'}
            />
          </div>

          {candidates.isLoading ? (
            <p className="empty">Loading…</p>
          ) : rows.length === 0 ? (
            <p className="empty">No patients are currently admitted.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Patient</th>
                  <th>Ward and bed</th>
                  <th>Care level</th>
                  <th>Days</th>
                  <th>Outstanding</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <CandidateRow
                    key={row.admission_id}
                    row={row}
                    selected={row.admission_id === selectedId}
                    onSelect={() =>
                      setSelectedId(row.admission_id === selectedId ? null : row.admission_id)
                    }
                    onClose={() => setSelectedId(null)}
                  />
                ))}
              </tbody>
            </table>
          )}

          <p className="hint">
            Ready for discharge means both required items are done: a doctor has cleared the
            patient, and the bill is settled.
          </p>
        </div>
      )}

      {!candidates.isError && (
        <div className="card">
          <h2>Discharged</h2>
          <p className="muted">
            Completed discharges. These are read-only records. Open one for the checklist, the
            sign-off and the bill as it was issued.
          </p>

          {candidates.isLoading ? (
            <p className="empty">Loading…</p>
          ) : finished.length === 0 ? (
            <p className="empty">No discharges recorded yet.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Patient</th>
                  <th>Ward and bed</th>
                  <th>Care level</th>
                  <th>Days</th>
                  <th>Discharged</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {finished.map((row) => (
                  <CandidateRow
                    key={row.admission_id}
                    row={row}
                    selected={row.admission_id === selectedId}
                    onSelect={() =>
                      setSelectedId(row.admission_id === selectedId ? null : row.admission_id)
                    }
                    onClose={() => setSelectedId(null)}
                  />
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </>
  );
}

function CandidateRow({
  row,
  selected,
  onSelect,
  onClose,
}: {
  row: DischargeCandidate;
  selected: boolean;
  onSelect: () => void;
  onClose: () => void;
}) {
  const outstanding = row.outstanding_items;

  return (
    <Fragment>
      <tr className={selected ? 'open' : undefined}>
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
        <td>{row.days_in_bed}</td>
        <td>
          {row.is_discharged ? (
            // Nothing is outstanding on a finished visit by definition - it could not have been
            // confirmed otherwise - so the column carries the useful fact instead.
            <span className="small">
              {row.discharged_at ? new Date(row.discharged_at).toLocaleString() : 'Discharged'}
            </span>
          ) : outstanding.length === 0 ? (
            <span className="badge status-available">Ready</span>
          ) : (
            <span className="small">{outstanding.map(checklistLabel).join(', ')}</span>
          )}
        </td>
        <td>
          <button type="button" className="secondary small" onClick={onSelect}>
            {selected ? 'Close' : 'Open'}
          </button>
        </td>
      </tr>

      {/*
        The detail as a row of this table, under the patient it describes, rather than a card at
        the foot of the page. colSpan has to match the header above or the drawer stops short of
        the last column and the table looks torn.
      */}
      {selected && (
        <tr className="drawer">
          <td colSpan={6}>
            <DischargeDetail
              admissionId={row.admission_id}
              onClose={onClose}
              onDischarged={onClose}
            />
          </td>
        </tr>
      )}
    </Fragment>
  );
}

// ---------------------------------------------------------------------------
// One patient's checklist
// ---------------------------------------------------------------------------

function DischargeDetail({
  admissionId,
  onClose,
  onDischarged,
}: {
  admissionId: string;
  onClose: () => void;
  onDischarged: () => void;
}) {
  const session = useSession();
  const role = session?.principal.role;
  const queryClient = useQueryClient();

  const [summaryNote, setSummaryNote] = useState('');

  const admission = useQuery(getAdmissionOptions({ path: { id: admissionId } }));

  // Every write here changes the admission, the candidate list, or both — a tick can move the
  // status to ready_for_discharge, and a confirm takes the row off the board entirely.
  const refreshAll = () =>
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

        return (
          id === 'listDischargeCandidates' ||
          id === 'getAdmission' ||
          id === 'listAdmissions' ||
          id === 'getWardCapacity' ||
          id === 'listOutstandingBills'
        );
      },
    });

  const tick = useMutation({
    ...updateDischargeChecklistMutation(),
    onSuccess: () => {
      toast.success('Checklist updated.');
      void refreshAll();
    },
  });

  const confirm = useMutation({
    ...confirmDischargeMutation(),
    onSuccess: () => {
      toast.success('Discharge confirmed. The bed is free again.');
      void refreshAll();
      onDischarged();
    },
  });

  if (admission.isLoading) {
    return (
      <div className="drawer-body">
        <p className="empty">Loading…</p>
      </div>
    );
  }

  if (admission.isError || !admission.data) {
    return (
      <div className="drawer-body">
        <div className="empty">
          <p>Could not load this admission.</p>
          <button type="button" className="secondary" onClick={() => void admission.refetch()}>
            Try again
          </button>
        </div>
      </div>
    );
  }

  const visit = admission.data;
  const checklist = visit.discharge?.checklist ?? {};

  // Fall back to the published vocabulary when no checklist row exists yet, so the boxes are
  // on screen before anybody has touched them. The server creates the rows on first tick.
  const items = checklistOrder.map((key) => ({
    key,
    item: checklist[key] as ChecklistItem | undefined,
  }));

  const cleared = items.find((entry) => entry.key === 'clinical_clearance');
  const settled = items.find((entry) => entry.key === 'billing_settled');

  const allMandatoryTicked = visit.discharge?.all_mandatory_ticked ?? false;
  const mayConfirm = canConfirmDischarge(role);
  const maySettle = canWorkBillingDesk(role);
  const isDischarged = visit.status === 'discharged';

  return (
    // The whole drawer prints, not just the charges table. A discharge document IS the bill:
    // who was cleared by which doctor, what it cost, who took the payment, who confirmed the
    // discharge and what the patient was told. Printing the charges alone produced a piece of
    // paper that proved a number and nothing else.
    <div className="drawer-body printable">
      <div className="print-only print-head">
        <h2>CareLanka Hospital</h2>
        <p>Discharge statement</p>
      </div>

      <div className="dialog-head no-print">
        <h3>
          {visit.patient?.full_name ?? 'Admission'}{' '}
          <span className="badge">{admissionCategoryLabels[visit.admission_category]}</span>
        </h3>
        <div className="actions">
          <button type="button" className="secondary small" onClick={() => window.print()}>
            Print / save
          </button>
          <button type="button" className="secondary small" onClick={onClose}>
            Close
          </button>
        </div>
      </div>

      {/* The same heading again for the printed copy, without the buttons. */}
      <h3 className="print-only">
        {visit.patient?.full_name}
        {visit.patient?.patient_code ? ` · ${visit.patient.patient_code}` : ''}
      </h3>

      <dl className="detail-grid">
        <div>
          <dt>Patient</dt>
          <dd>
            {visit.patient?.full_name ?? <span className="muted">Unknown</span>}
            <div className="small muted">
              {visit.patient?.patient_code}
              {visit.patient?.nic ? ` · ${visit.patient.nic}` : ''}
            </div>
          </dd>
        </div>
        <div>
          <dt>Ward and bed</dt>
          <dd>
            {visit.bed_number ? (
              <>
                {visit.ward_name}
                <div className="small muted">Bed {visit.bed_number}</div>
              </>
            ) : (
              <span className="muted">No bed</span>
            )}
          </dd>
        </div>
        <div>
          <dt>Care level</dt>
          <dd>{admissionCategoryLabels[visit.admission_category]}</dd>
        </div>
        <div>
          <dt>Admitted</dt>
          <dd>
            {visit.admitted_at ? (
              new Date(visit.admitted_at).toLocaleString()
            ) : (
              <span className="muted">Not recorded</span>
            )}
            {visit.category_set_by_staff_name && (
              <div className="small muted">by {visit.category_set_by_staff_name}</div>
            )}
          </dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>
            {admissionStatusLabels[visit.status]}
            {isDischarged && visit.discharged_at && (
              <div className="small muted">
                {new Date(visit.discharged_at).toLocaleString()}
              </div>
            )}
          </dd>
        </div>
      </dl>

      {/* ---------- the bill ---------- */}

      <h4>Charges</h4>

      <BillPanel admissionId={admissionId} canSettle={maySettle && !isDischarged} showPrint={false} />

      {/* ---------- who signed what ---------- */}
      {/*
        Below the bill and part of the same document, because that is what a discharge paper
        is. Three names and three times: the doctor who cleared the patient, the person who
        took the payment, and the person who confirmed the discharge.
      */}

      <h4>Sign-off</h4>

      <ChecklistRow
        item={cleared?.item}
        itemKey="clinical_clearance"
        mayTick={canTickChecklistItem(role, 'clinical_clearance') && !isDischarged}
        pending={tick.isPending}
        onToggle={(ticked) =>
          tick.mutate({ path: { admissionId }, body: { clinical_clearance: ticked } })
        }
      />

      <ChecklistRow item={settled?.item} itemKey="billing_settled" mayTick={false} />

      <dl className="detail-grid" style={{ marginTop: '0.9rem' }}>
        <div>
          <dt>Discharged by</dt>
          <dd>
            {visit.discharge?.confirmed_by_staff_name ?? (
              <span className="muted">Not yet</span>
            )}
            {visit.discharged_at && (
              <div className="small muted">
                {new Date(visit.discharged_at).toLocaleString()}
              </div>
            )}
          </dd>
        </div>
        {visit.discharge?.summary_note && (
          <div>
            <dt>Discharge instructions</dt>
            <dd>{visit.discharge.summary_note}</dd>
          </div>
        )}
      </dl>

      {/* ---------- the act itself ---------- */}

      <div className="no-print">
        {isDischarged ? (
          <p className="hint">This admission is closed. The record above cannot be changed.</p>
        ) : !mayConfirm ? (
          <p className="hint">
            Only reception, the ward nurse or the duty manager can confirm a discharge.
          </p>
        ) : (
          <>
            <div className="field">
              <label htmlFor="summary-note">
                Discharge instructions <span className="muted">(optional)</span>
              </label>
              <textarea
                id="summary-note"
                rows={3}
                maxLength={2000}
                value={summaryNote}
                placeholder="Rest for three days. Return if the swelling recurs."
                onChange={(event) => setSummaryNote(event.target.value)}
              />
              <p className="hint">Printed on the discharge statement.</p>
            </div>

            <button
              type="button"
              disabled={!allMandatoryTicked || confirm.isPending}
              onClick={() =>
                confirm.mutate({
                  path: { admissionId },
                  body: { summary_note: summaryNote.trim() || null },
                })
              }
            >
              {confirm.isPending ? 'Confirming…' : 'Confirm discharge'}
            </button>

            {!allMandatoryTicked && (
              <p className="hint">
                Available once a doctor has cleared the patient and the bill is settled.
                Confirming ends the admission and releases the bed.
              </p>
            )}
          </>
        )}
      </div>
    </div>
  );
}

/**
 * One line of the checklist: what it is, whether it is done, and a button only if this person
 * is the one who does it.
 *
 * Hidden rather than disabled. A control the user cannot use reads as a broken screen; an
 * absent one reads as somebody else's job. `billing_settled` passes `mayTick: false` from every
 * role, because settling the bill is what writes it and there is no second way.
 */
function ChecklistRow({
  item,
  itemKey,
  mayTick,
  pending,
  onToggle,
}: {
  item: ChecklistItem | undefined;
  itemKey: string;
  mayTick: boolean;
  pending?: boolean;
  onToggle?: (ticked: boolean) => void;
}) {
  const ticked = item?.ticked ?? false;
  const mandatory = item?.mandatory ?? mandatoryChecklistItems.has(itemKey);

  return (
    <div className="row" style={{ alignItems: 'flex-start' }}>
      <div className="field">
        <strong>{checklistLabel(itemKey)}</strong>
        {mandatory && <span className="badge">Required</span>}
        <div className="small muted">{checklistHints[itemKey]}</div>
      </div>
      <div className="field">
        {ticked ? (
          <>
            <span className="badge status-available">Done</span>
            <div className="small muted">
              {item?.ticked_by_staff_name && <strong>{item.ticked_by_staff_name}</strong>}
              {item?.ticked_at && ` · ${new Date(item.ticked_at).toLocaleString()}`}
            </div>
          </>
        ) : (
          <span className="muted">Not done</span>
        )}
      </div>
      <div style={{ alignSelf: 'center' }}>
        {mayTick && onToggle && (
          <button
            type="button"
            className="secondary small"
            disabled={pending}
            onClick={() => onToggle(!ticked)}
          >
            {ticked ? 'Undo' : 'Mark done'}
          </button>
        )}
      </div>
    </div>
  );
}

function Stat({
  caption,
  value,
  tone,
}: {
  caption: string;
  value: number;
  tone?: 'free' | 'none';
}) {
  return (
    <div className={tone ? `stat ${tone}` : 'stat'}>
      <div className="value">{value}</div>
      <div className="caption">{caption}</div>
    </div>
  );
}
