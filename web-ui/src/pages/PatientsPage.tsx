import { Fragment, useState } from 'react';
import type { FormEvent } from 'react';
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
import type { PrincipalRole, WorklistRow } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { MedicalProfilePanel } from '../components/MedicalProfilePanel';
import { BedSuggestionPanel } from '../components/BedSuggestionPanel';
import {
  canAssignBed,
  canCompleteVisit,
  canMarkArrived,
  canReadMedicalProfile,
  canReadPatientDetails,
} from '../types/permissions';
import { localDateTime } from '../types/datetime';
import { placementFor } from '../types/beds';
import type { Placement } from '../types/beds';
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
  const [suggestingId, setSuggestingId] = useState<string | null>(null);
  const [deskSuggesting, setDeskSuggesting] = useState(false);

  const [bedMode, setBedMode] = useState<'assign' | 'correct'>('assign');

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

  function openDetails(id: string) {
    setAssigningId(null);
    setSuggestingId(null);
    setOpenId((current) => (current === id ? null : id));
  }

  function openAssign(id: string, mode: 'assign' | 'correct' = 'assign') {
    setOpenId(null);
    setSuggestingId(null);
    setBedMode(mode);
    setAssigningId((current) => (current === id && bedMode === mode ? null : id));
  }

  function openSuggest(id: string) {
    setOpenId(null);
    setAssigningId(null);
    setDeskSuggesting(false);
    setSuggestingId((current) => (current === id ? null : id));
  }

  return (
    <>
      <h1>Patients</h1>
      <p className="muted">
        Everyone currently in the hospital's care: expected, waiting for a bed, in a bed, or
        finished. Open a row for the patient's details and times.
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
                  setSuggestingId(null);
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
                  setSuggestingId(null);
                }}
              />{' '}
              Include finished visits
            </label>
            <p className="hint">
              Also lists visits that are already discharged or cancelled.
            </p>
          </div>
        </form>
      </div>

      {canAssignBed(role) && (
        <div className="card">
          <h2>Suggest a bed</h2>
          <p className="muted">
            Off a slip at the desk, before the patient has a row on this board — type their NIC
            or patient code and the agent looks them up.
          </p>

          {deskSuggesting ? (
            <BedSuggestionPanel
              role={role}
              onAssigned={() => setDeskSuggesting(false)}
              onClose={() => setDeskSuggesting(false)}
            />
          ) : (
            <button type="button" onClick={() => setDeskSuggesting(true)}>
              Suggest a bed
            </button>
          )}
        </div>
      )}

      <div className="card">
        <h2>
          {includeFinished ? 'All patients, including finished visits' : 'Current patients'}
        </h2>

        {board.isLoading ? (
          <p className="empty">Loading…</p>
        ) : board.isError ? (
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
              : 'No patients are expected or admitted.'}
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Patient</th>

                <th>Arrived by</th>
                <th>Care level</th>

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

                      <code>{row.patient.patient_code}</code>
                      <br />
                      <span className="muted">
                        {patientIdentifier(row.patient) ?? 'No NIC on record'}
                      </span>
                    </td>

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
                        <span className="muted">Not recorded</span>
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
                        suggesting={suggestingId === row.id}
                        open={openId === row.id}
                        onAssign={(mode) => openAssign(row.id, mode)}
                        onSuggest={() => openSuggest(row.id)}
                        onDetails={() => openDetails(row.id)}
                      />
                    </td>
                  </tr>

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

                  {suggestingId === row.id && (
                    <tr className="drawer">
                      <td colSpan={5}>
                        <BedSuggestionPanel
                          admissionId={row.id}
                          role={role}
                          onAssigned={() => setSuggestingId(null)}
                          onClose={() => setSuggestingId(null)}
                        />
                      </td>
                    </tr>
                  )}

                  {openId === row.id && (
                    <tr className="drawer">
                      <td colSpan={5}>
                        <DetailsPanel row={row} role={role} onClose={() => setOpenId(null)} />
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

function RowActions({
  row,
  role,
  assigning,
  suggesting,
  open,
  onAssign,
  onSuggest,
  onDetails,
}: {
  row: WorklistRow;
  role: Parameters<typeof canAssignBed>[0];
  assigning: boolean;
  suggesting: boolean;
  open: boolean;
  onAssign: (mode: 'assign' | 'correct') => void;
  onSuggest: () => void;
  onDetails: () => void;
}) {
  const invalidate = useBoardInvalidation();

  const arrive = useMutation({
    ...markArrivedMutation(),
    onSuccess: () => {
      toast.success(`${row.patient.full_name} marked as arrived.`);
      invalidate();
    },
  });

  const complete = useMutation({
    ...completeVisitMutation(),
    onSuccess: () => {
      toast.success(`${row.patient.full_name}'s visit is complete.`);
      invalidate();
    },
  });

  const pending = arrive.isPending || complete.isPending;

  return (
    <>

      {row.status === 'awaiting_bed' && row.requires_bed && canAssignBed(role) && (
        <>
          <button type="button" onClick={() => onAssign('assign')}>
            {assigning ? 'Cancel' : 'Assign bed'}
          </button>{' '}
          <button type="button" className="secondary" onClick={onSuggest}>
            {suggesting ? 'Cancel' : 'Suggest bed'}
          </button>
        </>
      )}

      {(row.status === 'bed_ready' || row.status === 'admitted') &&
        row.requires_bed &&
        canAssignBed(role) && (
          <button type="button" className="secondary" onClick={() => onAssign('correct')}>
            {assigning ? 'Cancel' : 'Change bed'}
          </button>
        )}

      {row.status === 'bed_ready' && canMarkArrived(role) && (
        <button
          type="button"
          disabled={pending}
          onClick={() => arrive.mutate({ path: { id: row.id } })}
        >
          {arrive.isPending ? 'Saving…' : 'Mark arrived'}
        </button>
      )}

      {row.status === 'admitted' && !row.requires_bed && canCompleteVisit(role) && (
        <button
          type="button"
          disabled={pending}
          onClick={() => complete.mutate({ path: { id: row.id } })}
        >
          {complete.isPending ? 'Saving…' : 'Complete visit'}
        </button>
      )}{' '}
      <button type="button" className="secondary" onClick={onDetails}>
        {open ? 'Hide' : 'Details'}
      </button>
    </>
  );
}

function AssignBedPanel({
  row,
  onDone,
  mode = 'assign',
}: {
  row: WorklistRow;
  onDone: () => void;

  mode?: 'assign' | 'correct';
}) {
  const session = useSession();
  const invalidate = useBoardInvalidation();
  const [reason, setReason] = useState('');
  const correcting = mode === 'correct';

  const visit = useQuery(getAdmissionOptions({ path: { id: row.id } }));
  const wards = useQuery(listWardsOptions({}));

  const beds = useQuery(
    listBedAvailabilityOptions({ query: { availability: 'free', pageSize: 500 } }),
  );

  const alreadyHere =
    visit.data?.source === 'walk_in' && canMarkArrived(session?.principal.role);

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
        `${assignment.ward_name} · ${assignment.bed_number} is held for ` +
          `${row.patient.full_name}. The hold expires if they are not marked as arrived.`,
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
          'The previous bed is free again and is not charged for.',
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
    placement: visit.data
      ? placementFor(
          bed,
          wardsById.get(bed.ward_id),
          visit.data,
          { gender: row.patient.gender, date_of_birth: row.patient.date_of_birth },
          session?.principal.role,
        )
      : ({ kind: 'refused', why: 'Loading…' } as Placement),
  }));

  const usable = candidates.filter((candidate) => candidate.placement.kind !== 'refused');
  const overrides = candidates.filter((candidate) => candidate.placement.kind === 'override');

  const missing = (beds.data?.total_items ?? 0) - (beds.data?.items?.length ?? 0);
  const loading = visit.isLoading || wards.isLoading || beds.isLoading;
  const failed = visit.isError || wards.isError || beds.isError;

  return (
    <div className="drawer-body">
      <h3>
        {correcting ? 'Change bed for' : 'Assign a bed to'} {row.patient.full_name}
      </h3>
      <p className="muted">
        {correcting ? (
          <>
            The current bed returns to the board and is <strong>not charged for</strong>. The
            patient&rsquo;s status and time in a bed carry over, so the bill is unaffected.
            <br />
            For a bed chosen by mistake only. A genuine ward transfer is not built yet, and
            using this instead would write off a night&rsquo;s bed charge.
          </>
        ) : alreadyHere ? (
          <>
            The patient is at the desk, so assigning a bed admits them to it directly. Their
            stay and their bill start now.
          </>
        ) : (
          <>
            Assigning a bed holds it for thirty minutes, and a ward nurse confirms the patient
            is in it. If nobody does, the hold expires on its own and the bed is released.
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
          No beds are free. The patient stays on the waiting list until one is — capacity is the
          duty manager's to resolve.
        </p>
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>Bed</th>
                <th>Ward</th>
                <th>Accepts</th>
                <th>Isolation</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {candidates.map(({ bed, ward, placement }) => (
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
                    {placement.kind === 'refused' ? (
                      <span className="muted">{placement.why}</span>
                    ) : (
                      <>
                        <button
                          type="button"
                          className={placement.kind === 'override' ? 'warn' : undefined}
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
                          {placement.kind === 'override'
                            ? correcting
                              ? 'Move anyway'
                              : 'Assign anyway'
                            : correcting
                              ? 'Move here'
                              : alreadyHere
                                ? 'Assign and admit'
                                : 'Assign'}
                        </button>
                        {placement.kind === 'override' && (
                          <p className="hint" style={{ marginTop: '0.25rem' }}>
                            {placement.why}
                          </p>
                        )}
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {usable.length === 0 && (
            <p className="empty">
              Beds are free, but none of them accepts this patient. The reason is on each row.
            </p>
          )}

          {missing > 0 && (
            <p className="field-error" style={{ marginTop: '0.6rem' }}>
              {missing} more free {missing === 1 ? 'bed is' : 'beds are'} not shown. This list
              is incomplete — report it before assigning a bed from it.
            </p>
          )}

          {overrides.length > 0 && (
            <p className="hint" style={{ marginTop: '0.6rem' }}>
              Amber buttons are beds outside the patient&rsquo;s care level. You may use one as
              duty manager; it is recorded as your decision, so give a reason in the note.
            </p>
          )}

          <div className="field" style={{ marginTop: '0.9rem' }}>
            <label htmlFor="override-reason">
              {correcting ? 'Reason for the change' : 'Note'} (optional)
            </label>
            <input
              id="override-reason"
              value={reason}
              maxLength={500}
              onChange={(event) => setReason(event.target.value)}
              placeholder={
                correcting ? 'Wrong row selected' : 'Why this bed rather than another'
              }
            />
            <p className="hint">
              {correcting
                ? 'Kept on the record. Not required.'
                : 'Kept on the record. Once the bed agent is running, this is where you record why its suggestion was not followed.'}
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

function DetailsPanel({
  row,
  role,
  onClose,
}: {
  row: WorklistRow;
  role: PrincipalRole | undefined;
  onClose: () => void;
}) {
  const patient = useQuery(getPatientOptions({ path: { id: row.patient.id } }));

  const visit = useQuery(getAdmissionOptions({ path: { id: row.id } }));

  const liveBed = visit.data?.bed_assignments?.find(
    (assignment) => assignment.status !== 'released',
  );

  return (
    <div className="drawer-body">
      <h3>Patient details</h3>
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
            <Field label="Mobile app account">
              {patient.data.has_account ? 'Linked' : 'None'}
            </Field>
          </tbody>
        </table>
      )}

      <>
        <h4 style={{ marginTop: '1.25rem', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
          This visit
        </h4>
          {visit.isLoading && <p className="empty">Loading…</p>}
          {visit.data && (
            <table>
              <tbody>
                <Field label="Arrived by">{admissionSourceLabels[visit.data.source]}</Field>
                <Field label="Care level">
                  {admissionCategoryLabels[visit.data.admission_category]}
                </Field>
                <Field label="Urgency">{admissionUrgencyLabels[visit.data.urgency]}</Field>
                <Field label="Needs a bed">
                  {visit.data.requires_bed ? 'Yes' : 'No — outpatient, no bed is held'}
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
                <Field label="Bed assigned by" empty="No bed assigned">
                  {liveBed?.approved_by_staff_name ?? (liveBed ? 'Not recorded' : null)}
                </Field>
                <Field label="Expected at">
                  {visit.data.expected_arrival
                    ? localDateTime(visit.data.expected_arrival)
                    : null}
                </Field>
                <Field label="Arrived" empty="Not yet">
                  {visit.data.admitted_at ? localDateTime(visit.data.admitted_at) : null}
                </Field>
                <Field label="Discharged">
                  {visit.data.discharged_at ? localDateTime(visit.data.discharged_at) : null}
                </Field>
              </tbody>
            </table>
          )}

          {visit.data && visit.data.missing_fields.length > 0 && (
            <>
              <p style={{ marginTop: '0.9rem' }}>
                <strong>Details still missing:</strong>
              </p>
              <ul>
                {visit.data.missing_fields.map((field) => (
                  <li key={field}>{detailFieldLabel(field)}</li>
                ))}
              </ul>
            </>
          )}
      </>

      {canReadMedicalProfile(role) && (
        <>
          <h4 style={{ marginTop: '1.25rem', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
            Medical details
          </h4>
          <MedicalProfilePanel
            patientId={row.patient.id}
            patientName={row.patient.full_name}
            role={role}
          />
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
