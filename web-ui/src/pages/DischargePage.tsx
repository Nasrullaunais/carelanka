import { useState } from 'react';
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
  canConfirmDischargeOf,
  canTickChecklistItem,
  canWorkBillingDesk,
  canWorkDischargeChecklist,
} from '../types/permissions';
import { admissionCategoryLabels } from '../types/patients';
import {
  checklistHints,
  checklistLabel,
  checklistOrder,
  mandatoryChecklistItems,
} from '../types/billing';

// Sending a patient home. The left half is who could go; the right half is the one checklist
// that decides it, and the sign-off.
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
// The order down the page is the order the job is done in: a doctor says the patient is well
// enough, the bill is filled in and paid, and then somebody sends them home. It used to be a
// table of five boxes with the bill on a different screen, and "where does the billing happen?"
// was a fair question to ask of it.

export function DischargePage() {
  const session = useSession();
  const role = session?.principal.role;
  const canWork = canWorkDischargeChecklist(role);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  // The hook cannot go behind an early return, so it is switched off instead. Nobody who
  // cannot work the checklist should be asking the server at all.
  const candidates = useQuery({
    ...listDischargeCandidatesOptions({ query: { pageSize: 50 } }),
    enabled: canWork,
  });

  if (!canWork) {
    return (
      <>
        <h1>Discharge</h1>
        <p className="empty">
          Discharges are worked by ward nurses, doctors and the duty manager.
        </p>
      </>
    );
  }

  const rows = candidates.data?.items ?? [];
  const ready = rows.filter((row) => row.outstanding_items.length === 0);

  return (
    <>
      <h1>Discharge</h1>
      <p className="muted">
        Everyone currently in a bed, with what is still outstanding before they can go. Being on
        this list changes nothing on its own — a person confirms the discharge.
      </p>

      {candidates.isError ? (
        <div className="card">
          <div className="empty">
            <p>Could not load the ward.</p>
            <button type="button" className="secondary" onClick={() => void candidates.refetch()}>
              Try again
            </button>
          </div>
        </div>
      ) : (
        <div className="card">
          <h2>On the ward</h2>

          <div className="stats">
            <Stat caption="In a bed" value={rows.length} />
            <Stat
              caption="Ready to go"
              value={ready.length}
              tone={ready.length === 0 ? 'none' : 'free'}
            />
          </div>

          {candidates.isLoading ? (
            <p className="empty">Loading…</p>
          ) : rows.length === 0 ? (
            <p className="empty">Nobody is admitted at the moment.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Patient</th>
                  <th>Where</th>
                  <th>Care level</th>
                  <th>Days</th>
                  <th>Still outstanding</th>
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
                  />
                ))}
              </tbody>
            </table>
          )}

          <p className="hint">
            &ldquo;Ready to go&rdquo; means both required things are done — a doctor has cleared
            the patient, and the bill is settled. Nothing else holds a discharge up.
          </p>
        </div>
      )}

      {selectedId && (
        <DischargeDetail
          admissionId={selectedId}
          onClose={() => setSelectedId(null)}
          onDischarged={() => setSelectedId(null)}
        />
      )}
    </>
  );
}

function CandidateRow({
  row,
  selected,
  onSelect,
}: {
  row: DischargeCandidate;
  selected: boolean;
  onSelect: () => void;
}) {
  const outstanding = row.outstanding_items;

  return (
    <tr className={selected ? 'selected' : undefined}>
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
        {outstanding.length === 0 ? (
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
      toast.success('Discharged. The bed is free again.');
      void refreshAll();
      onDischarged();
    },
  });

  if (admission.isLoading) {
    return (
      <div className="card">
        <p className="empty">Loading…</p>
      </div>
    );
  }

  if (admission.isError || !admission.data) {
    return (
      <div className="card">
        <div className="empty">
          <p>Could not open that visit.</p>
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
  const mayConfirm = canConfirmDischargeOf(role, visit.admission_category);
  const maySettle = canWorkBillingDesk(role);
  const isDischarged = visit.status === 'discharged';

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>
          {visit.patient?.full_name ?? 'Visit'}{' '}
          <span className="badge">{admissionCategoryLabels[visit.admission_category]}</span>
        </h2>
        <button type="button" className="secondary small" onClick={onClose}>
          Close
        </button>
      </div>

      <dl className="detail-grid">
        <dt>Where</dt>
        <dd>{visit.bed_number ? `${visit.ward_name} · bed ${visit.bed_number}` : 'No bed'}</dd>
        <dt>Status</dt>
        <dd>{visit.status.replaceAll('_', ' ')}</dd>
        <dt>Admitted by</dt>
        <dd>
          {visit.category_set_by_staff_name ?? <span className="muted">Unknown</span>}
          {visit.category_set_at && (
            <div className="small muted">
              {new Date(visit.category_set_at).toLocaleString()}
            </div>
          )}
        </dd>
      </dl>

      {/* ---------- 1. the wall ---------- */}

      <h3>1 · Cleared by a doctor</h3>

      <ChecklistRow
        item={cleared?.item}
        itemKey="clinical_clearance"
        mayTick={canTickChecklistItem(role, 'clinical_clearance') && !isDischarged}
        pending={tick.isPending}
        onToggle={(ticked) =>
          tick.mutate({ path: { admissionId }, body: { clinical_clearance: ticked } })
        }
      />

      {/* ---------- 2. the money ---------- */}

      <h3>2 · The bill</h3>

      <BillPanel admissionId={admissionId} canSettle={maySettle && !isDischarged} />

      {/* ---------- 3. what settling wrote ---------- */}

      <h3>3 · Bill settled</h3>

      <ChecklistRow item={settled?.item} itemKey="billing_settled" mayTick={false} />

      {/* ---------- 4. send them home ---------- */}

      <h3>4 · Send them home</h3>

      {isDischarged ? (
        <p className="empty">
          Already discharged
          {visit.discharged_at && ` at ${new Date(visit.discharged_at).toLocaleString()}`}
          {visit.discharge?.confirmed_by_staff_name &&
            ` by ${visit.discharge.confirmed_by_staff_name}`}
          .
        </p>
      ) : !mayConfirm ? (
        <p className="empty">
          {visit.admission_category === 'icu' || visit.admission_category === 'hdu'
            ? 'An ICU or HDU discharge is the duty manager’s decision.'
            : 'Confirming a discharge is the ward nurse’s or the duty manager’s.'}
        </p>
      ) : (
        <>
          <div className="field">
            <label htmlFor="summary-note">
              What the patient takes home with them <span className="muted">(optional)</span>
            </label>
            <textarea
              id="summary-note"
              rows={3}
              maxLength={2000}
              value={summaryNote}
              placeholder="Rest for three days. Come back if the swelling returns."
              onChange={(event) => setSummaryNote(event.target.value)}
            />
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
            {confirm.isPending ? 'Discharging…' : 'Confirm discharge'}
          </button>

          {!allMandatoryTicked && (
            <p className="hint">
              Disabled until steps 1 and 3 are both done. This ends the visit, frees the bed and
              sends someone home, so it does not happen halfway.
            </p>
          )}
        </>
      )}
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
          <span className="muted">Not yet</span>
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
            {ticked ? 'Untick' : 'Tick'}
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
