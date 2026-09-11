import { useState } from 'react';
import type { FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  getAdmissionOptions,
  getPatientOptions,
  listAdmissionsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { AdmissionSummary } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canReadAdmissions } from '../types/permissions';
import { localDateTime } from '../types/datetime';
import {
  admissionCategoryLabels,
  admissionSourceLabels,
  admissionStatusLabels,
  admissionStatuses,
  admissionUrgencyLabels,
  detailFieldLabel,
  genderLabels,
  patientIdentifier,
} from '../types/patients';

// Who is in the hospital, and everything the desk recorded about them.
//
// The list is admissions rather than patients on purpose: a patient is a permanent record,
// a visit is what is happening now. Somebody registered years ago and at home today is not
// a current patient, and a ward board that showed them would be useless by the second week.

const PAGE_SIZE = 20;

export function PatientsPage() {
  const session = useSession();

  const [search, setSearch] = useState('');
  const [submitted, setSubmitted] = useState('');
  const [includeFinished, setIncludeFinished] = useState(false);
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);

  // Gated rather than skipped: the hook cannot go behind the early return, and without this
  // an administrator opening the URL fires a request that 403s and toasts red.
  const canRead = canReadAdmissions(session?.principal.role);

  const admissions = useQuery({
    ...listAdmissionsOptions({
      query: {
        // No status at all means the live worklist. The full list is how you ask for the
        // archive as well — the endpoint has no "everything" flag.
        ...(includeFinished ? { status: admissionStatuses } : {}),
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
          Your role cannot read the admissions list. Ward nurses, doctors and the duty manager
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
  }

  const rows = admissions.data?.items ?? [];
  const open = rows.find((row) => row.id === openId) ?? null;

  return (
    <>
      <h1>Patients</h1>
      <p className="muted">
        Everyone with a visit open right now — how they came in, what care level they were put
        at, and where they are up to. Open a row for their intake details and times.
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
                placeholder="Name or NIC"
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
                }}
              />{' '}
              Include finished visits
            </label>
            <p className="hint">
              Off, this is who is in the hospital now. On, it adds everyone discharged or
              cancelled as well.
            </p>
          </div>
        </form>
      </div>

      <div className="card">
        <h2>{includeFinished ? 'Every visit' : 'In the hospital now'}</h2>

        {admissions.isLoading ? (
          <p className="empty">Loading…</p>
        ) : admissions.isError ? (
          // The toast already fired. "Try again" rather than an empty table, which would read
          // as an empty hospital when it is really a broken request.
          <div className="empty">
            <p>Could not load the patient list.</p>
            <button type="button" className="secondary" onClick={() => void admissions.refetch()}>
              Try again
            </button>
          </div>
        ) : rows.length === 0 ? (
          <p className="empty">
            {submitted
              ? `Nobody with an open visit matches “${submitted}”.`
              : 'Nobody has an open visit.'}
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Patient</th>
                <th>Came in</th>
                <th>Care level</th>
                <th>Status</th>
                <th>Bed</th>
                <th>Arrived</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td>
                    <strong>{row.patient?.full_name ?? 'Unknown'}</strong>
                    <br />
                    <span className="muted">
                      {(row.patient && patientIdentifier(row.patient)) ?? 'No NIC on record'}
                    </span>
                  </td>
                  <td>{admissionSourceLabels[row.source]}</td>
                  <td>
                    {admissionCategoryLabels[row.admission_category]}
                    <br />
                    <span className="muted">{admissionUrgencyLabels[row.urgency]}</span>
                  </td>
                  <td>
                    <span className={row.status === 'admitted' ? 'badge' : 'badge retired'}>
                      {admissionStatusLabels[row.status]}
                    </span>
                  </td>
                  <td>
                    {row.ward_name ? (
                      `${row.ward_name} · ${row.bed_number}`
                    ) : (
                      <span className="muted">Not assigned yet</span>
                    )}
                  </td>
                  <td>
                    {row.admitted_at ? (
                      localDateTime(row.admitted_at)
                    ) : (
                      <span className="muted">Not yet</span>
                    )}
                  </td>
                  <td>
                    <button
                      type="button"
                      className="secondary"
                      onClick={() => setOpenId((current) => (current === row.id ? null : row.id))}
                    >
                      {openId === row.id ? 'Hide' : 'Details'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {admissions.data && admissions.data.total_items > 0 && (
          <div className="row" style={{ marginTop: '0.9rem', alignItems: 'center' }}>
            <p className="muted" style={{ flex: '2 1 14rem' }}>
              {admissions.data.total_items} visit
              {admissions.data.total_items === 1 ? '' : 's'}, page {admissions.data.page} of{' '}
              {admissions.data.total_pages}
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
              disabled={page >= admissions.data.total_pages}
              onClick={() => setPage((current) => current + 1)}
            >
              Next
            </button>
          </div>
        )}
      </div>

      {open && <DetailsCard admission={open} onClose={() => setOpenId(null)} />}
    </>
  );
}

// ---------------------------------------------------------------------------
// One person, in full
// ---------------------------------------------------------------------------

function DetailsCard({
  admission,
  onClose,
}: {
  admission: AdmissionSummary;
  onClose: () => void;
}) {
  // Two calls because they are two different records. The patient is who they are and does
  // not change between visits; the admission is this visit, and it carries the times.
  const patient = useQuery({
    ...getPatientOptions({ path: { id: admission.patient?.id ?? '' } }),
    enabled: Boolean(admission.patient?.id),
  });
  const visit = useQuery(getAdmissionOptions({ path: { id: admission.id } }));

  return (
    <div className="card">
      <h2>{admission.patient?.full_name ?? 'Unknown patient'}</h2>

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

      <h3 style={{ marginTop: '1.25rem' }}>This visit</h3>
      {visit.isLoading && <p className="empty">Loading…</p>}
      {visit.data && (
        <table>
          <tbody>
            <Field label="Came in as">{admissionSourceLabels[visit.data.source]}</Field>
            <Field label="Care level">
              {admissionCategoryLabels[visit.data.admission_category]}
            </Field>
            <Field label="Urgency">{admissionUrgencyLabels[visit.data.urgency]}</Field>
            <Field label="Status">{admissionStatusLabels[visit.data.status]}</Field>
            <Field label="Needs isolation">{visit.data.is_infectious ? 'Yes' : 'No'}</Field>
            <Field label="Bed" empty="Not assigned yet">
              {visit.data.ward_name ? `${visit.data.ward_name} · ${visit.data.bed_number}` : null}
            </Field>
            <Field label="Record opened">
              {visit.data.created_at ? localDateTime(visit.data.created_at) : null}
            </Field>
            <Field label="Care level chosen">{localDateTime(visit.data.category_set_at)}</Field>
            <Field label="Expected">
              {visit.data.expected_arrival ? localDateTime(visit.data.expected_arrival) : null}
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
            <strong>Paperwork still outstanding:</strong>
          </p>
          <ul>
            {visit.data.missing_fields.map((field) => (
              <li key={field}>{detailFieldLabel(field)}</li>
            ))}
          </ul>
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
