import { Fragment, useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  assignBedManuallyMutation,
  correctBedMutation,
  completeVisitMutation,
  getAdmissionOptions,
  getPatientOptions,
  listBedAvailabilityOptions,
  listPatientWorklistOptions,
  listWardsOptions,
  markArrivedMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { WorklistRow } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import {
  canAssignBed,
  canCompleteVisit,
  canMarkArrived,
  canReadPatientDetails,
} from '../types/permissions';
import { localDateTime } from '../types/datetime';
import { whyNotPlaceable } from '../types/beds';
import { genderPolicyLabels, wardTypeLabels } from '../types/wards';
import {
  arrivalRouteLabel,
  worklistStatusDetail,
  worklistStatusLabels,
  worklistStatusTone,
} from '../types/worklist';
import {
  admissionCategoryLabels,
  admissionSourceLabels,
  admissionUrgencyLabels,
  detailFieldLabel,
  genderLabels,
  patientIdentifier,
} from '../types/patients';

// The ward board: who the hospital is dealing with, and what is happening with them.
//
// One row per person's business, from two tables. A booking nobody has checked in is a row
// reading "Not arrived"; once they are checked in the same person is one row reading
// "Admitted", because the booking is finished with and its visit stands for it.
//
// This used to list admissions only, and an admission is created by *arriving* — so the board
// could never say "not arrived" about anybody, and the woman booked in for a scan at eleven
// was invisible until she walked through the door.

const PAGE_SIZE = 20;

export function PatientsPage() {
  const session = useSession();
  const role = session?.principal.role;

  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [includeFinished, setIncludeFinished] = useState(false);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);
  const [assigningId, setAssigningId] = useState<string | null>(null);

  // Which job the bed drawer is doing. Same panel, same rules, two different endpoints behind
  // it: picking a first bed, or swapping one that was chosen by mistake.
  const [bedMode, setBedMode] = useState<'assign' | 'correct'>('assign');

  // Gated rather than skipped: the hook cannot go behind the early return, and without this
  // an administrator opening the URL fires a request that 403s and toasts red.
  const canRead = canReadPatientDetails(role);

  const board = useQuery({
    ...listPatientWorklistOptions({
      query: {
        includeFinished,
        ...(submitted ? { search: submitted } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
    enabled: canRead,
  });

  if (!canRead) {
    return (
      <>
        <h1>Patients</h1>
        <p className="empty">
          Your role cannot read the patients board. Ward nurses, doctors and the duty manager
          can.
        </p>
      </>
    );
  }

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setSubmitted(search.trim());
    setPage(1);
    setOpenId(null);
    setAssigningId(null);
  }

  const rows = board.data?.items ?? [];

  // One drawer at a time. Both open at once would stack two panels under one person and push
  // the next patient a screen and a half down — the exact thing the drawer is here to stop.
  function openDetails(id: string) {
    setAssigningId(null);
    setOpenId((current) => (current === id ? null : id));
  }

  function openAssign(id: string, mode: 'assign' | 'correct' = 'assign') {
    setOpenId(null);
    setBedMode(mode);
    setAssigningId((current) => (current === id && bedMode === mode ? null : id));
  }

  return (
    <>
      <h1>Patients</h1>
      <p className="muted">
        Everyone the hospital is dealing with right now — booked in and not here yet, waiting
        for a bed, in a bed, or done. Open a row for their intake details and times.
      </p>

      <div className="card">
        <form onSubmit={submitSearch}>
          <div className="row">
            <div className="field">
              <label htmlFor="patient-search">Search</label>
              <input
                id="patient-search"
                value={search}
                maxLength={100}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Patient ID, name or NIC"
              />
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', marginBottom: '0.85rem' }}>
              <button type="submit">Search</button>
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', marginBottom: '0.85rem' }}>
              <button
                type="button"
                className="secondary"
                disabled={search === '' && submitted === ''}
                onClick={() => {
                  setSearch('');
                  setSubmitted('');
                  setPage(1);
                  setOpenId(null);
                  setAssigningId(null);
                }}
              >
                Clear
              </button>
            </div>
          </div>

          <div className="field">
            <label htmlFor="include-finished">
              <input
                id="include-finished"
                type="checkbox"
                checked={includeFinished}
                onChange={(event) => {
                  setIncludeFinished(event.target.checked);
                  setPage(1);
                  setOpenId(null);
                  setAssigningId(null);
                }}
              />{' '}
              Include finished visits
            </label>
            <p className="hint">
              Off, this is today's business. On, it adds everyone already discharged or
              cancelled as well.
            </p>
          </div>
        </form>
      </div>

      <div className="card">
        <h2>{includeFinished ? 'Everyone, including finished' : 'On the board now'}</h2>

        {board.isLoading ? (
          <p className="empty">Loading…</p>
        ) : board.isError ? (
          // The toast already fired. "Try again" rather than an empty table, which would read
          // as an empty hospital when it is really a broken request.
          <div className="empty">
            <p>Could not load the patients board.</p>
            <button type="button" className="secondary" onClick={() => void board.refetch()}>
              Try again
            </button>
          </div>
        ) : rows.length === 0 ? (
          <p className="empty">
            {submitted
              ? `Nobody on the board matches “${submitted}”.`
              : 'Nobody is booked in or in the hospital.'}
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Patient</th>
                {/* Renamed from "Came in". It answers how they got here, which is not the same
                    question as what they are here for — that is the care level. */}
                <th>Arrived by</th>
                <th>Care level</th>
                {/* One column, not two. Status and Bed used to sit side by side and between
                    them say almost nothing: "Awaiting bed" appeared against a patient in for a
                    blood test who was never going to be given one, and Bed was blank for
                    everybody without one yet. The bed now explains the status instead. */}
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <Fragment key={row.id}>
                  <tr
                    className={openId === row.id || assigningId === row.id ? 'open' : undefined}
                  >
                    <td>
                      <strong>{row.patient.full_name}</strong>
                      <br />
                      {/* The patient ID first, because it is the one thing about this person
                          that every other part of the hospital asks for — Equipment's screen
                          wants it before it will hand out a drip stand. */}
                      <code>{row.patient.patient_code}</code>
                      <br />
                      <span className="muted">
                        {patientIdentifier(row.patient) ?? 'No NIC on record'}
                      </span>
                    </td>
                    {/* Always one of three routes, a booking included — making an
                        appointment is the route. Whether they have turned up is the Status
                        column's job, and it used to be answered here too, as "Not yet". */}
                    <td>{arrivalRouteLabel(row)}</td>
                    <td>
                      {row.admission_category ? (
                        <>
                          {admissionCategoryLabels[row.admission_category]}
                          <br />
                          <span className="muted">
                            {row.urgency ? admissionUrgencyLabels[row.urgency] : ''}
                          </span>
                        </>
                      ) : (
                        // Genuinely unknown, not empty: the care level is chosen by staff at
                        // check-in, never by the patient at booking time. The reason they gave
                        // is the one thing anybody does know, so it goes here.
                        <span className="muted">
                          Set at check-in
                          {row.reason ? (
                            <>
                              <br />
                              For: {row.reason}
                            </>
                          ) : null}
                        </span>
                      )}
                    </td>
                    <td>
                      <span className={worklistStatusTone(row.status)}>
                        {worklistStatusLabels[row.status]}
                      </span>
                      {worklistStatusDetail(row) && (
                        <>
                          <br />
                          <span className="muted">{worklistStatusDetail(row)}</span>
                        </>
                      )}
                    </td>
                    <td>
                      <RowActions
                        row={row}
                        role={role}
                        assigning={assigningId === row.id}
                        open={openId === row.id}
                        onAssign={(mode) => openAssign(row.id, mode)}
                        onDetails={() => openDetails(row.id)}
                      />
                    </td>
                  </tr>

                  {/*
                    The detail as a row of this table, under the person it describes, rather
                    than a card at the foot of the page. colSpan has to match the header above
                    or the drawer stops short of the last column and the table looks torn.
                  */}
                  {assigningId === row.id && (
                    <tr className="drawer">
                      <td colSpan={5}>
                        <AssignBedPanel
                          row={row}
                          mode={bedMode}
                          onDone={() => setAssigningId(null)}
                        />
                      </td>
                    </tr>
                  )}

                  {openId === row.id && (
                    <tr className="drawer">
                      <td colSpan={5}>
                        <DetailsPanel row={row} onClose={() => setOpenId(null)} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}

        {board.data && board.data.total_items > 0 && (
          <div className="row" style={{ marginTop: '0.9rem', alignItems: 'center' }}>
            <p className="muted" style={{ flex: '2 1 14rem' }}>
              {board.data.total_items} row{board.data.total_items === 1 ? '' : 's'}, page{' '}
              {board.data.page} of {board.data.total_pages}
            </p>
            <button
              type="button"
              className="secondary"
              style={{ flex: '0 0 auto' }}
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
            >
              Previous
            </button>
            <button
              type="button"
              className="secondary"
              style={{ flex: '0 0 auto' }}
              disabled={page >= board.data.total_pages}
              onClick={() => setPage((current) => current + 1)}
            >
              Next
            </button>
          </div>
        )}
      </div>
    </>
  );
}

/**
 * Invalidates every list a write to one visit can change. A mutation invalidates what it
 * changed, not only what is on screen — the board, the admissions list, the bed availability
 * list and both capacity reads all move when one patient gets or gives up a bed.
 */
function useBoardInvalidation() {
  const queryClient = useQueryClient();

  return () =>
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

        return (
          id === 'listPatientWorklist' ||
          id === 'listAdmissions' ||
          id === 'listAppointments' ||
          id === 'getAdmission' ||
          id === 'listBedAvailability' ||
          id === 'getWardCapacity' ||
          id === 'getWardOccupancy'
        );
      },
    });
}

// ---------------------------------------------------------------------------
// What you can do to one row
// ---------------------------------------------------------------------------

/**
 * Every control is hidden rather than disabled when it does not apply, and what applies is
 * read off the row rather than guessed.
 *
 * The one that used to be wrong: "Assign bed" appeared for anybody in `awaiting_bed`, which
 * was everybody, including the patient here for a blood test. `requires_bed` is now published
 * by the API precisely so this decision is not a second copy of the rule.
 */
function RowActions({
  row,
  role,
  assigning,
  open,
  onAssign,
  onDetails,
}: {
  row: WorklistRow;
  role: Parameters<typeof canAssignBed>[0];
  assigning: boolean;
  open: boolean;
  onAssign: (mode: 'assign' | 'correct') => void;
  onDetails: () => void;
}) {
  const invalidate = useBoardInvalidation();

  const arrive = useMutation({
    ...markArrivedMutation(),
    onSuccess: () => {
      toast.success(`${row.patient.full_name} is in the bed.`);
      invalidate();
    },
  });

  const complete = useMutation({
    ...completeVisitMutation(),
    onSuccess: () => {
      toast.success(`${row.patient.full_name} is finished and can go home.`);
      invalidate();
    },
  });

  const pending = arrive.isPending || complete.isPending;

  return (
    <>
      {/* Not arrived: the act that moves this row is check-in, and check-in is the bookings
          desk's screen — it needs a care level, an urgency and an isolation answer, which is a
          form and not a button. Sent there rather than half-built here. */}
      {row.status === 'not_arrived' && (
        <Link to="/appointments" className="muted" style={{ fontSize: '0.82rem' }}>
          Check in at the desk
        </Link>
      )}

      {row.status === 'awaiting_bed' && row.requires_bed && canAssignBed(role) && (
        <button type="button" onClick={() => onAssign('assign')}>
          {assigning ? 'Cancel' : 'Assign bed'}
        </button>
      )}

      {/* Beds get mis-clicked, and the alternative to fixing one is a ward board that is known
          to be wrong - which is a board people stop reading. Offered from the moment they hold
          a bed right up until they leave. */}
      {(row.status === 'bed_ready' || row.status === 'admitted') &&
        row.requires_bed &&
        canAssignBed(role) && (
          <button type="button" className="secondary" onClick={() => onAssign('correct')}>
            {assigning ? 'Cancel' : 'Wrong bed?'}
          </button>
        )}

      {/* The hold lapses in thirty minutes, so this is the row with a clock on it. */}
      {row.status === 'bed_ready' && canMarkArrived(role) && (
        <button
          type="button"
          disabled={pending}
          onClick={() => arrive.mutate({ path: { id: row.id } })}
        >
          {arrive.isPending ? 'Saving…' : 'They are in the bed'}
        </button>
      )}

      {/* Only a visit that never needed a bed. Finishing one that has a bed is a discharge —
          checklist, summary note, approver, and the bed given back — and the server refuses it
          here with cl_pat_020. Hidden rather than offered and refused. */}
      {row.status === 'admitted' && !row.requires_bed && canCompleteVisit(role) && (
        <button
          type="button"
          disabled={pending}
          onClick={() => complete.mutate({ path: { id: row.id } })}
        >
          {complete.isPending ? 'Saving…' : 'Mark completed'}
        </button>
      )}{' '}
      <button type="button" className="secondary" onClick={onDetails}>
        {open ? 'Hide' : 'Details'}
      </button>
    </>
  );
}

// ---------------------------------------------------------------------------
// Picking a bed by hand
// ---------------------------------------------------------------------------

/**
 * The manual path, and it must always work: if the only way to admit a patient were through
 * the AI, the hospital would stop the moment the AI stopped.
 *
 * Three reads, because "which bed can this patient go in" is not one table. `bed-availability`
 * is Equipment's register joined with our assignments; `listWards` says what kind of ward each
 * bed stands in, which is what hard rules H2 and H3 turn on; and the admission detail carries
 * `is_infectious`, which the board row does not.
 */
function AssignBedPanel({
  row,
  onDone,
  mode = 'assign',
}: {
  row: WorklistRow;
  onDone: () => void;
  /** `correct` swaps the bed they are already in for one that was chosen by mistake. */
  mode?: 'assign' | 'correct';
}) {
  const session = useSession();
  const invalidate = useBoardInvalidation();
  const [reason, setReason] = useState('');
  const correcting = mode === 'correct';

  const visit = useQuery(getAdmissionOptions({ path: { id: row.id } }));
  const wards = useQuery(listWardsOptions({}));

  // Free only. A held or occupied bed is not a candidate, and `free` is where the thirty-minute
  // expiry is applied — a bed whose hold has lapsed is offered again with nobody having
  // released it.
  const beds = useQuery(
    listBedAvailabilityOptions({ query: { availability: 'free', pageSize: 100 } }),
  );

  // A walk-in is standing at the desk, so choosing their bed and saying they are in it are
  // the same act to the person doing it. Two buttons for it was one button too many - and until
  // the second was pressed the thirty-minute hold could take the bed back from a patient lying
  // in it.
  //
  // Two calls and not one, because the API keeps them separate on purpose: somebody expected
  // later gets a bed kept EMPTY for them, and /arrive is what says they turned up. Chaining
  // here rather than merging them server-side leaves that distinction where it belongs. If the
  // second call fails the first still stands - the patient holds the bed, the button reappears,
  // and the hold expiry is the backstop.
  const alreadyHere = visit.data?.source === 'walk_in';

  const arrive = useMutation({
    ...markArrivedMutation(),
    onSuccess: () => {
      invalidate();
      onDone();
    },
  });

  const assign = useMutation({
    ...assignBedManuallyMutation(),
    onSuccess: (assignment) => {
      if (alreadyHere) {
        toast.success(
          `${row.patient.full_name} is in ${assignment.ward_name} · ${assignment.bed_number}.`,
        );

        arrive.mutate({ path: { id: row.id } });
        return;
      }

      toast.success(
        `${assignment.ward_name} · ${assignment.bed_number} is being held for ` +
          `${row.patient.full_name}. The hold lapses if they are not marked as arrived.`,
      );

      invalidate();
      onDone();
    },
  });

  const correct = useMutation({
    ...correctBedMutation(),
    onSuccess: (assignment) => {
      toast.success(
        `Moved to ${assignment.ward_name} · ${assignment.bed_number}. ` +
          'The old bed is free again and is charged for nothing.',
      );

      invalidate();
      onDone();
    },
  });

  const writing = assign.isPending || arrive.isPending || correct.isPending;

  const wardsById = new Map((wards.data ?? []).map((ward) => [ward.id, ward]));

  const candidates = (beds.data?.items ?? []).map((bed) => ({
    bed,
    ward: wardsById.get(bed.ward_id),
    why: visit.data
      ? whyNotPlaceable(
          bed,
          wardsById.get(bed.ward_id),
          visit.data,
          row.patient.gender,
          session?.principal.role,
        )
      : 'Loading…',
  }));

  const usable = candidates.filter((candidate) => candidate.why === null);
  const loading = visit.isLoading || wards.isLoading || beds.isLoading;
  const failed = visit.isError || wards.isError || beds.isError;

  return (
    <div className="drawer-body">
      <h3>
        {correcting ? 'A different bed for' : 'A bed for'} {row.patient.full_name}
      </h3>
      <p className="muted">
        {correcting ? (
          <>
            The bed they are in now goes back on the board and is{' '}
            <strong>charged for nothing</strong>, because it was never really theirs. Their
            status and the time they have been in a bed both carry over, so the bill is
            unaffected.
            <br />
            This is for a bed picked by mistake. A patient genuinely moving ward is a transfer,
            which is not built yet — using this for one would give a night&rsquo;s bed away.
          </>
        ) : alreadyHere ? (
          <>
            They are at the desk, so choosing a bed puts them straight into it — one act,
            not two. Their stay, and the bill, start now.
          </>
        ) : (
          <>
            Choosing a bed holds it for thirty minutes. If the patient is not marked as arrived
            by then the hold lapses on its own and the bed goes back to whoever needs it —
            nobody has to undo anything.
          </>
        )}
      </p>

      {loading ? (
        <p className="empty">Loading…</p>
      ) : failed ? (
        <div className="empty">
          <p>Could not load the free beds.</p>
          <button
            type="button"
            className="secondary"
            onClick={() => {
              void visit.refetch();
              void wards.refetch();
              void beds.refetch();
            }}
          >
            Try again
          </button>
        </div>
      ) : candidates.length === 0 ? (
        <p className="empty">
          No bed in the hospital is free. The patient stays waiting for one — that is a real
          answer, not a failure, and it is the duty manager's to solve.
        </p>
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>Bed</th>
                <th>Ward</th>
                <th>Takes</th>
                <th>Isolation</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {candidates.map(({ bed, ward, why }) => (
                <tr key={bed.id}>
                  <td>
                    <strong>{bed.bed_number}</strong>
                  </td>
                  <td>
                    {bed.ward_name}
                    <br />
                    <span className="muted">
                      {ward ? wardTypeLabels[ward.ward_type] : 'Unknown ward'}
                    </span>
                  </td>
                  <td>{ward ? genderPolicyLabels[ward.gender_policy] : '—'}</td>
                  <td>{bed.has_isolation ? 'Yes' : 'No'}</td>
                  <td>
                    {why === null ? (
                      <button
                        type="button"
                        disabled={writing}
                        onClick={() =>
                          correcting
                            ? correct.mutate({
                                path: { id: row.id },
                                body: {
                                  bed_id: bed.id,
                                  reason: reason.trim().length > 0 ? reason.trim() : undefined,
                                },
                              })
                            : assign.mutate({
                                path: { id: row.id },
                                body: {
                                  bed_id: bed.id,
                                  override_reason:
                                    reason.trim().length > 0 ? reason.trim() : undefined,
                                },
                              })
                        }
                      >
                        {correcting ? 'Move here' : alreadyHere ? 'Put them here' : 'Choose'}
                      </button>
                    ) : (
                      // Listed with its reason rather than hidden. A ward nurse looking at an
                      // empty ICU needs to see that the beds are there and who to ask.
                      <span className="muted">{why}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {usable.length === 0 && (
            <p className="empty">
              Beds are free, but none of them will take this patient. The reason is on each row.
            </p>
          )}

          <div className="field" style={{ marginTop: '0.9rem' }}>
            <label htmlFor="override-reason">
              {correcting ? 'What went wrong' : 'Note'} (optional)
            </label>
            <input
              id="override-reason"
              value={reason}
              maxLength={500}
              onChange={(event) => setReason(event.target.value)}
              placeholder={
                correcting ? 'Picked the row above' : 'Why this bed rather than another'
              }
            />
            <p className="hint">
              {correcting
                ? 'Kept on the record. Not required - somebody fixing their own mis-click ten seconds later has nothing useful to write.'
                : 'Kept on the record. Once the bed agent is running this is where you say why you ignored what it suggested.'}
            </p>
          </div>
        </>
      )}

      <button type="button" className="secondary" style={{ marginTop: '0.9rem' }} onClick={onDone}>
        Close
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// One person, in full
// ---------------------------------------------------------------------------

function DetailsPanel({ row, onClose }: { row: WorklistRow; onClose: () => void }) {
  // Two calls because they are two different records. The patient is who they are and does
  // not change between visits; the admission is this visit, and it carries the times.
  //
  // A booking has no admission to fetch — the visit has not started — so the second call is
  // skipped rather than fired at an id that belongs to an appointment and would 404.
  const patient = useQuery(getPatientOptions({ path: { id: row.patient.id } }));

  const visit = useQuery({
    ...getAdmissionOptions({ path: { id: row.id } }),
    enabled: row.kind === 'visit',
  });

  // The assignment they are in now. Released rows are kept as history, so "who put them here"
  // is the first one that is NOT released, not simply the first one in the list.
  const liveBed = visit.data?.bed_assignments?.find(
    (assignment) => assignment.status !== 'released',
  );

  return (
    <div className="drawer-body">
      <h3>Intake details</h3>
      {patient.isLoading && <p className="empty">Loading…</p>}
      {patient.data && (
        <table>
          <tbody>
            <Field label={patient.data.nic ? 'NIC' : 'Reference'}>
              {patientIdentifier(patient.data)}
            </Field>
            <Field label="Gender">{genderLabels[patient.data.gender]}</Field>
            <Field label="Date of birth">{patient.data.date_of_birth}</Field>
            <Field label="Phone">{patient.data.phone}</Field>
            <Field label="Address">{patient.data.address}</Field>
            <Field label="Emergency contact">
              {patient.data.emergency_contact_name
                ? `${patient.data.emergency_contact_name} · ${
                    patient.data.emergency_contact_phone ?? 'no number'
                  }`
                : null}
            </Field>
            <Field label="Registered">
              {patient.data.created_at ? localDateTime(patient.data.created_at) : null}
            </Field>
            <Field label="App login">{patient.data.has_account ? 'Linked' : 'None'}</Field>
          </tbody>
        </table>
      )}

      {row.kind === 'booking' ? (
        <>
          <h4 style={{ marginTop: '1.25rem', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
            This booking
          </h4>
          <table>
            <tbody>
              <Field label="Due">{localDateTime(row.when)}</Field>
              <Field label="Reason" empty="Not given">
                {row.reason}
              </Field>
            </tbody>
          </table>
          <p className="hint">
            There is no visit record yet. Checking them in at the bookings desk is what creates
            one, and the care level is chosen there — by the person at the desk, never by the
            patient when they booked.
          </p>
        </>
      ) : (
        <>
          <h4 style={{ marginTop: '1.25rem', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
            This visit
          </h4>
          {visit.isLoading && <p className="empty">Loading…</p>}
          {visit.data && (
            <table>
              <tbody>
                <Field label="Came in as">{admissionSourceLabels[visit.data.source]}</Field>
                <Field label="Care level">
                  {admissionCategoryLabels[visit.data.admission_category]}
                </Field>
                <Field label="Urgency">{admissionUrgencyLabels[visit.data.urgency]}</Field>
                <Field label="Needs a bed">
                  {visit.data.requires_bed
                    ? 'Yes'
                    : 'No — seen and sent home, so no bed is ever held'}
                </Field>
                <Field label="Needs isolation">{visit.data.is_infectious ? 'Yes' : 'No'}</Field>
                <Field
                  label="Bed"
                  empty={visit.data.requires_bed ? 'Not assigned yet' : 'None needed'}
                >
                  {visit.data.ward_name
                    ? `${visit.data.ward_name} · ${visit.data.bed_number}`
                    : null}
                </Field>
                <Field label="Record opened">
                  {visit.data.created_at ? localDateTime(visit.data.created_at) : null}
                </Field>
                <Field label="Admitted by" empty="Not recorded">
                  {visit.data.category_set_by_staff_name}
                </Field>
                <Field label="Care level chosen">
                  {localDateTime(visit.data.category_set_at)}
                </Field>
                <Field label="Bed given by" empty="No bed assigned">
                  {liveBed?.approved_by_staff_name ?? (liveBed ? 'Not recorded' : null)}
                </Field>
                <Field label="Expected">
                  {visit.data.expected_arrival
                    ? localDateTime(visit.data.expected_arrival)
                    : null}
                </Field>
                <Field label="Arrived" empty="Not yet">
                  {visit.data.admitted_at ? localDateTime(visit.data.admitted_at) : null}
                </Field>
                <Field label="Finished">
                  {visit.data.discharged_at ? localDateTime(visit.data.discharged_at) : null}
                </Field>
              </tbody>
            </table>
          )}

          {visit.data && visit.data.missing_fields.length > 0 && (
            <>
              <p style={{ marginTop: '0.9rem' }}>
                <strong>Paperwork still outstanding:</strong>
              </p>
              <ul>
                {visit.data.missing_fields.map((field) => (
                  <li key={field}>{detailFieldLabel(field)}</li>
                ))}
              </ul>
            </>
          )}
        </>
      )}

      <button
        type="button"
        className="secondary"
        style={{ marginTop: '0.9rem' }}
        onClick={onClose}
      >
        Close
      </button>
    </div>
  );
}

/**
 * One row of a detail table. Anything null says so in words rather than leaving a blank cell,
 * because a blank reads as a rendering bug. `empty` overrides the wording where "not recorded"
 * would be wrong — a bed nobody has assigned yet is not a missing field.
 */
function Field({
  label,
  empty = 'Not recorded',
  children,
}: {
  label: string;
  empty?: string;
  children: React.ReactNode;
}) {
  return (
    <tr>
      <th scope="row">{label}</th>
      <td>{children ?? <span className="muted">{empty}</span>}</td>
    </tr>
  );
}
