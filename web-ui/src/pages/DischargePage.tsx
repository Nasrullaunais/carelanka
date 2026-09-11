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
import { useSession } from '../services/auth/useSession';
import {
  canConfirmDischargeOf,
  canTickChecklistItem,
  canWorkDischargeChecklist,
} from '../types/permissions';
import { admissionCategoryLabels } from '../types/patients';
import {
  checklistHints,
  checklistLabel,
  checklistOrder,
  mandatoryChecklistItems,
  money,
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
//   "Bill settled" has no button for anybody. It is written by settling the bill on the
//   Billing screen and nowhere else, so the money and the tick are one fact rather than two
//   that can drift apart.

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
            &ldquo;Ready to go&rdquo; means every mandatory box is ticked — a doctor's clearance,
            the medication, and the bill. Follow-up and transport are useful but do not hold
            anybody up.
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
  const bill = visit.bill;

  // Fall back to the published vocabulary when no checklist row exists yet, so the boxes are
  // on screen before anybody has touched them. The server creates the rows on first tick.
  const items = checklistOrder.map((key) => ({
    key,
    item: checklist[key] as ChecklistItem | undefined,
  }));

  const allMandatoryTicked = visit.discharge?.all_mandatory_ticked ?? false;
  const mayConfirm = canConfirmDischargeOf(role, visit.admission_category);
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
        <dd>
          {visit.bed_number ? `${visit.ward_name} · bed ${visit.bed_number}` : 'No bed'}
        </dd>
        <dt>Status</dt>
        <dd>{visit.status.replaceAll('_', ' ')}</dd>
        <dt>Bill</dt>
        <dd>
          {bill ? (
            <>
              {money(bill.total, bill.currency)} ·{' '}
              {bill.settled ? (
                <span className="badge status-available">Settled</span>
              ) : (
                <span className="badge severity-high">Not settled</span>
              )}
              <div className="small muted">{bill.bill_number}</div>
            </>
          ) : (
            <span className="muted">Not prepared yet</span>
          )}
        </dd>
      </dl>

      <h3>Checklist</h3>

      <table>
        <thead>
          <tr>
            <th>Item</th>
            <th>State</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {items.map(({ key, item }) => {
            const ticked = item?.ticked ?? false;
            const mandatory = item?.mandatory ?? mandatoryChecklistItems.has(key);
            const mayTick = canTickChecklistItem(role, key) && !isDischarged;

            return (
              <tr key={key}>
                <td>
                  <strong>{checklistLabel(key)}</strong>
                  {mandatory && <span className="badge">Required</span>}
                  <div className="small muted">{checklistHints[key]}</div>
                </td>
                <td>
                  {ticked ? (
                    <>
                      <span className="badge status-available">Done</span>
                      {item?.ticked_at && (
                        <div className="small muted">
                          {new Date(item.ticked_at).toLocaleString()}
                        </div>
                      )}
                    </>
                  ) : (
                    <span className="muted">Not yet</span>
                  )}
                </td>
                <td>
                  {/* Hidden, not disabled. A control the user cannot use reads as a broken
                      screen; an absent one reads as somebody else's job. */}
                  {mayTick && (
                    <button
                      type="button"
                      className="secondary small"
                      disabled={tick.isPending}
                      onClick={() =>
                        tick.mutate({
                          path: { admissionId },
                          body: { [key]: !ticked },
                        })
                      }
                    >
                      {ticked ? 'Untick' : 'Tick'}
                    </button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      <p className="hint">
        Ticking the last required box moves this visit to <strong>ready for discharge</strong>;
        unticking one moves it back. &ldquo;Bill settled&rdquo; is the exception — it is written
        by settling the bill on the Billing screen, so the money and the box are one fact.
      </p>

      <h3>Confirm</h3>

      {isDischarged ? (
        <p className="empty">
          Already discharged{visit.discharged_at && ` at ${new Date(visit.discharged_at).toLocaleString()}`}.
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
              Disabled until every required box is ticked. This ends the visit, frees the bed and
              sends someone home, so it does not happen halfway.
            </p>
          )}
        </>
      )}
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
