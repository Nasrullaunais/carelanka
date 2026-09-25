import { PaginationControls } from '../components/ui/pagination-controls';
import { Table } from '../components/Table';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  assignBedManuallyMutation,
  correctBedMutation,
  getAdmissionOptions,
  getPatientOptions,
  listBedAvailabilityOptions,
  listPatientWorklistOptions,
  listWardsOptions,
  markArrivedMutation,
  updatePatientMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PrincipalRole, WorklistRow } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { MedicalProfilePanel } from '../components/MedicalProfilePanel';
import { ActionDialog } from '../components/ui/action-dialog';
import { BedCandidateTable } from '../components/BedCandidateTable';
import {
  canAssignBed,
  canEditPatient,
  canMarkArrived,
  canReadMedicalProfile,
  canReadPatientDetails,
} from '../types/permissions';
import {
  PatientFields,
  emptyPatientForm,
  onSubmit,
  patientFormBody,
  patientFormFrom,
  patientFormProblems,
  usePatientForm,
} from './intake-form';
import { localDateTime } from '../types/datetime';
import { placementFor } from '../types/beds';
import type { Placement } from '../types/beds';
import {
  arrivalRouteLabel,
  worklistStatusDetail,
  worklistStatusLabels,
  worklistStatusTone,
} from '../types/worklist';
import {
  admissionCategoryLabels,
  admissionSourceLabels,
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

  const [searchParams, setSearchParams] = useSearchParams();
  const pendingAssignId = searchParams.get('assignBed');

  const pendingAdmission = useQuery({
    ...getAdmissionOptions({ path: { id: pendingAssignId ?? '' } }),
    enabled: canRead && pendingAssignId !== null,
  });

  // Coming straight from an emergency admission: search for that patient's row so it is on
  // the current page, then fall through to the effect below that opens the assign panel.
  useEffect(() => {
    const code = pendingAdmission.data?.patient?.patient_code;

    if (!pendingAssignId || !code) {
      return;
    }

    setSearch(code);
    setSubmitted(code);
    setPage(1);
  }, [pendingAssignId, pendingAdmission.data]);

  useEffect(() => {
    const row = (board.data?.items ?? []).find((item) => item.id === pendingAssignId);

    if (!pendingAssignId || !row || !canAssignBed(role)) {
      return;
    }

    setOpenId(null);
    setBedMode('assign');
    setAssigningId(row.id);

    const next = new URLSearchParams(searchParams);
    next.delete('assignBed');
    setSearchParams(next, { replace: true });
  }, [pendingAssignId, board.data, role, searchParams, setSearchParams]);

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
              Also lists visits that are already discharged or cancelled.
            </p>
          </div>
        </form>
      </div>

      <div className="table-section">
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
          <Table footer={<PaginationControls label="Patients" page={page} totalPages={board.data?.total_pages ?? 1} totalItems={board.data?.total_items} onPageChange={setPage} />}>
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
                  <tr
                    key={row.id}
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
                          {row.is_infectious && (
                            <>
                              <br />
                              <span className="badge severity-high">Needs isolation</span>
                            </>
                          )}
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
                        open={openId === row.id}
                        onAssign={(mode) => openAssign(row.id, mode)}
                        onDetails={() => openDetails(row.id)}
                      />
                    </td>
                  </tr>

              ))}
            </tbody>
          </Table>
        )}
      </div>
      <ActionDialog title={`${bedMode === 'correct' ? 'Correct bed' : 'Assign bed'} · ${rows.find((row) => row.id === assigningId)?.patient.full_name ?? 'patient'}`} isOpen={assigningId != null} onClose={() => setAssigningId(null)}>
        {rows.find((row) => row.id === assigningId) && <AssignBedPanel row={rows.find((row) => row.id === assigningId)!} mode={bedMode} onDone={() => setAssigningId(null)} />}
      </ActionDialog>
      <ActionDialog title={`Patient details · ${rows.find((row) => row.id === openId)?.patient.full_name ?? 'patient'}`} isOpen={openId != null} onClose={() => setOpenId(null)}>
        {rows.find((row) => row.id === openId) && <DetailsPanel row={rows.find((row) => row.id === openId)!} role={role} onClose={() => setOpenId(null)} />}
      </ActionDialog>
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
      toast.success(`${row.patient.full_name} marked as arrived.`);
      invalidate();
    },
  });

  const pending = arrive.isPending;

  return (
    <>

      {row.status === 'awaiting_bed' && canAssignBed(role) && (
        <button type="button" onClick={() => onAssign('assign')}>
          {assigning ? 'Cancel' : 'Assign bed'}
        </button>
      )}

      {(row.status === 'bed_ready' || row.status === 'admitted') &&
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
{' '}
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
          <BedCandidateTable
            candidates={candidates}
            writing={writing}
            missing={missing}
            actionLabel={(placement) =>
              placement.kind === 'override'
                ? correcting
                  ? 'Move anyway'
                  : 'Assign anyway'
                : correcting
                  ? 'Move here'
                  : alreadyHere
                    ? 'Assign and admit'
                    : 'Assign'
            }
            onPick={(bed) =>
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
                      override_reason: reason.trim().length > 0 ? reason.trim() : undefined,
                    },
                  })
            }
          />

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

  const [editing, setEditing] = useState(false);

  const liveBed = visit.data?.bed_assignments?.find(
    (assignment) => assignment.status !== 'released',
  );

  return (
    <div className="drawer-body">
      <h3>Patient details</h3>
      {patient.isLoading && <p className="empty">Loading…</p>}

      {patient.data && editing && (
        <EditPatientPanel
          patientId={patient.data.id}
          identified={patient.data.nic !== null}
          onDone={() => setEditing(false)}
        />
      )}

      {patient.data && !editing && (
        <>
          <Table>
            <tbody>
              <Field label={patient.data.nic ? 'NIC' : 'Reference'}>
                {patientIdentifier(patient.data)}
              </Field>
              <Field label="Gender">{genderLabels[patient.data.gender]}</Field>
              <Field label="Date of birth">{patient.data.date_of_birth}</Field>
              <Field label="Phone">{patient.data.phone}</Field>
              <Field label="Address">{patient.data.address}</Field>
              <Field label="Emergency/guardian contact">
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
          </Table>

          {canEditPatient(role) && (
            <button
              type="button"
              style={{ marginTop: '0.6rem' }}
              onClick={() => setEditing(true)}
            >
              Edit patient details
            </button>
          )}
        </>
      )}

      <>
        <h4 style={{ marginTop: '1.25rem', marginBottom: '0.5rem', fontSize: '0.9rem' }}>
          This visit
        </h4>
          {visit.isLoading && <p className="empty">Loading…</p>}
          {visit.data && (
            <Table>
              <tbody>
                <Field label="Arrived by">{admissionSourceLabels[visit.data.source]}</Field>
                <Field label="Care level" empty="Not yet classified">
                  {visit.data.admission_category
                    ? admissionCategoryLabels[visit.data.admission_category]
                    : null}
                </Field>
                <Field label="Needs isolation">{visit.data.is_infectious ? 'Yes' : 'No'}</Field>
                <Field label="Bed" empty="Not assigned yet">
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
                <Field label="Care level chosen" empty="Not yet classified">
                  {visit.data.category_set_at
                    ? localDateTime(visit.data.category_set_at)
                    : null}
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
            </Table>
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

function EditPatientPanel({
  patientId,
  identified,
  onDone,
}: {
  patientId: string;
  identified: boolean;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();

  const existing = useQuery(getPatientOptions({ path: { id: patientId } }));

  const form = usePatientForm(emptyPatientForm(false));
  const problems = patientFormProblems(form.value, identified, existing.data?.nic ?? '');

  const [loadedId, setLoadedId] = useState<string | null>(null);

  if (existing.data && loadedId !== existing.data.id) {
    setLoadedId(existing.data.id);
    form.replace(patientFormFrom(existing.data));
  }

  const save = useMutation({
    ...updatePatientMutation(),
    onSuccess: (saved) => {
      toast.success(`${saved.full_name} updated.`);

      queryClient.invalidateQueries({
        predicate: (query) => {
          const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;

          return id === 'listPatients' || id === 'getPatient' || id === 'listPatientWorklist';
        },
      });

      onDone();
    },
  });

  if (existing.isLoading || !existing.data) {
    return <p className="empty">Loading…</p>;
  }

  const patient = existing.data;

  return (
    <form
      onSubmit={onSubmit(() =>
        save.mutate({
          path: { id: patientId },
          body: patientFormBody(form.value, patient.nic ?? null),
        }),
      )}
    >
      <PatientFields
        value={form.value}
        set={form.set}
        idPrefix="board-edit"
        identified={identified}
        allowUnknownGender={!identified}
        nic={patient.nic ?? ''}
      />

      <div className="row">
        <button type="submit" disabled={save.isPending || problems.blocked}>
          {save.isPending ? 'Saving...' : 'Save'}
        </button>
        <button type="button" className="secondary" onClick={onDone}>
          Cancel
        </button>
      </div>
    </form>
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
