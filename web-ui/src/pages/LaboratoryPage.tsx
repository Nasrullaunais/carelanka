import { useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  listLabPatientsOptions,
  listLabReportsOptions,
  listPatientsOptions,
  listWardsOptions,
  uploadLabReportMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import { downloadLabReport } from '../services/api/generated';
import type { LabPatient, LabReport } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canFileLabReport } from '../types/permissions';

const PAGE_SIZE = 10;

/** 10 MB, the same number the API enforces. Checked here so a slow upload is not how you find out. */
const MAX_BYTES = 10 * 1024 * 1024;

const ACCEPTED = 'application/pdf,image/jpeg,image/png';

/** What the lab selected, however they found it. A report is filed against a patient, not a visit. */
type Chosen = { id: string; patientCode: string; fullName: string; whereabouts?: string };

/**
 * The laboratory desk. Pick a ward, read down it, and file a result against whoever the specimen
 * came from.
 *
 * The ward list is the way in because that is how a lab works: a rack of specimens arrives from
 * one ward and gets worked through in order. Search by code, name or NIC stays as the second way
 * in, for an outpatient in for a blood test who is in no ward at all.
 */
export function LaboratoryPage() {
  const session = useSession();
  const mayFile = canFileLabReport(session?.principal.role);

  const [selected, setSelected] = useState<Chosen | null>(null);

  return (
    <>
      <h1>Laboratory</h1>
      <p className="muted">
        File a finished result against the patient it belongs to. The ward can read it the moment
        you upload it, instead of waiting for the paper copy to arrive.
      </p>

      <WardBrowser selected={selected} onSelect={setSelected} />
      <PatientSearch selected={selected} onSelect={setSelected} />

      {selected && (
        <>
          <ReportList patient={selected} />
          {mayFile && <UploadCard patient={selected} />}
        </>
      )}
    </>
  );
}

function WardBrowser({
  selected,
  onSelect,
}: {
  selected: Chosen | null;
  onSelect: (patient: Chosen) => void;
}) {
  // Empty means every ward, which is also the answer for an outpatient holding no bed at all.
  const [wardName, setWardName] = useState('');
  const [page, setPage] = useState(1);

  const wards = useQuery(listWardsOptions({ query: { isActive: true } }));

  const patients = useQuery(
    listLabPatientsOptions({
      query: {
        ...(wardName.length > 0 ? { wardName } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
  );

  const rows = patients.data?.items ?? [];
  const totalPages = patients.data?.total_pages ?? 1;

  return (
    <div className="card">
      <h2>Patients in the hospital now</h2>

      <label htmlFor="lab-ward">Ward</label>
      <select
        id="lab-ward"
        value={wardName}
        onChange={(event) => {
          setWardName(event.target.value);
          setPage(1);
        }}
      >
        <option value="">Every ward</option>
        {(wards.data ?? []).map((ward) => (
          <option key={ward.id} value={ward.name}>
            {ward.name}
          </option>
        ))}
      </select>

      {patients.isPending && <p className="muted">Loading patients…</p>}

      {patients.isError && (
        <p className="empty">
          Could not load the ward list.{' '}
          <button type="button" className="secondary" onClick={() => void patients.refetch()}>
            Try again
          </button>
        </p>
      )}

      {patients.isSuccess && rows.length === 0 && (
        <p className="empty">
          {wardName.length > 0
            ? `Nobody is in ${wardName} at the moment.`
            : 'Nobody is currently admitted.'}
        </p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Ward</th>
              <th>Bed</th>
              <th>Code</th>
              <th>Name</th>
              <th>Visit</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((patient) => (
              <tr key={patient.patient_id}>
                <td>{patient.ward_name ?? <span className="muted">No bed</span>}</td>
                <td>{patient.bed_number ?? <span className="muted">—</span>}</td>
                <td>{patient.patient_code}</td>
                <td>{patient.full_name}</td>
                <td>{admissionStatusLabel(patient)}</td>
                <td>
                  <button type="button" onClick={() => onSelect(toChosen(patient))}>
                    {selected?.id === patient.patient_id ? 'Selected' : 'Open'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {totalPages > 1 && (
        <div className="pager">
          <button
            type="button"
            className="secondary"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
          >
            Previous
          </button>
          <span className="muted">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className="secondary"
            disabled={page >= totalPages}
            onClick={() => setPage((current) => current + 1)}
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}

/**
 * The second way in, for somebody who is not on a ward list: an outpatient in for a blood test,
 * or a visit that has already ended. Nothing is fetched until two characters are typed.
 */
function PatientSearch({
  selected,
  onSelect,
}: {
  selected: Chosen | null;
  onSelect: (patient: Chosen) => void;
}) {
  const [search, setSearch] = useState('');

  const patients = useQuery({
    ...listPatientsOptions({ query: { search: search.trim(), pageSize: PAGE_SIZE } }),
    enabled: search.trim().length >= 2,
  });

  return (
    <div className="card">
      <h2>Or search by code, name or NIC</h2>
      <p className="muted">
        For somebody who is not on a ward list — an outpatient in for a blood test, or a visit
        that has already ended.
      </p>

      <label htmlFor="lab-search">Patient code, name or NIC</label>
      <input
        id="lab-search"
        value={search}
        placeholder="P7K2X9QM"
        onChange={(event) => setSearch(event.target.value)}
      />

      {patients.isPending && search.trim().length >= 2 && <p className="muted">Searching…</p>}

      {patients.isSuccess && patients.data.items.length === 0 && (
        <p className="empty">Nobody matches that. Check the label on the specimen.</p>
      )}

      {patients.isSuccess && patients.data.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>NIC</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {patients.data.items.map((patient) => (
              <tr key={patient.id}>
                <td>{patient.patient_code}</td>
                <td>{patient.full_name}</td>
                <td>{patient.nic ?? <span className="muted">None recorded</span>}</td>
                <td>
                  <button
                    type="button"
                    onClick={() =>
                      onSelect({
                        id: patient.id,
                        patientCode: patient.patient_code,
                        fullName: patient.full_name,
                      })
                    }
                  >
                    {selected?.id === patient.id ? 'Selected' : 'Open'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function toChosen(patient: LabPatient): Chosen {
  return {
    id: patient.patient_id,
    patientCode: patient.patient_code,
    fullName: patient.full_name,
    whereabouts:
      patient.ward_name === null || patient.ward_name === undefined
        ? undefined
        : `${patient.ward_name}, bed ${patient.bed_number ?? '—'}`,
  };
}

/** Plain words for where the visit has got to. "awaiting_bed" is not a thing anybody says. */
function admissionStatusLabel(patient: LabPatient): string {
  switch (patient.admission_status) {
    case 'admitted':
      return 'In a bed';
    case 'awaiting_bed':
      return 'Waiting for a bed';
    case 'awaiting_approval':
      return 'Bed awaiting approval';
    case 'bed_reserved':
      return 'Bed held';
    case 'ready_for_discharge':
      return 'Going home';
    default:
      return patient.admission_status;
  }
}

function ReportList({ patient }: { patient: Chosen }) {
  const [page, setPage] = useState(1);

  const reports = useQuery(
    listLabReportsOptions({ query: { patientId: patient.id, page, pageSize: PAGE_SIZE } }),
  );

  const rows = reports.data?.items ?? [];
  const totalPages = reports.data?.total_pages ?? 1;

  return (
    <div className="card">
      <h2>
        Results for {patient.fullName} ({patient.patientCode})
      </h2>
      {patient.whereabouts && <p className="muted">{patient.whereabouts}</p>}

      {reports.isPending && <p className="muted">Loading results…</p>}

      {reports.isError && (
        <p className="empty">
          Could not load results.{' '}
          <button type="button" className="secondary" onClick={() => void reports.refetch()}>
            Try again
          </button>
        </p>
      )}

      {reports.isSuccess && rows.length === 0 && (
        <p className="empty">Nothing filed for this patient yet.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Test</th>
              <th>Filed</th>
              <th>Summary</th>
              <th>File</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((report) => (
              <tr key={report.id}>
                <td>{report.test_name}</td>
                <td>{new Date(report.created_at).toLocaleString()}</td>
                <td>{report.summary ?? <span className="muted">None written</span>}</td>
                <td>
                  {report.file_name} <span className="muted">({kilobytes(report.byte_size)})</span>
                </td>
                <td>
                  <OpenReportButton report={report} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {totalPages > 1 && (
        <div className="pager">
          <button
            type="button"
            className="secondary"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
          >
            Previous
          </button>
          <span className="muted">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className="secondary"
            disabled={page >= totalPages}
            onClick={() => setPage((current) => current + 1)}
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}

/**
 * Opening a report is a fetch, not a link. The file needs the bearer token, and an anchor
 * cannot carry a header — a plain href would answer 401. So the bytes come back through the
 * generated client and the browser is handed an object URL for them.
 */
function OpenReportButton({ report }: { report: LabReport }) {
  const [opening, setOpening] = useState(false);

  return (
    <button
      type="button"
      className="secondary"
      disabled={opening}
      onClick={async () => {
        setOpening(true);

        try {
          const { data, error } = await downloadLabReport({ path: { id: report.id } });

          // The transport interceptor has already toasted whatever went wrong.
          if (error || !data) {
            return;
          }

          const url = URL.createObjectURL(data as Blob);
          window.open(url, '_blank', 'noopener');

          // The tab has the bytes now. Held briefly rather than revoked immediately, because
          // revoking before the new tab has read it gives a blank viewer.
          setTimeout(() => URL.revokeObjectURL(url), 60_000);
        } finally {
          setOpening(false);
        }
      }}
    >
      {opening ? 'Opening…' : 'Open'}
    </button>
  );
}

function UploadCard({ patient }: { patient: Chosen }) {
  const queryClient = useQueryClient();
  const [testName, setTestName] = useState('');
  const [summary, setSummary] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [tooBig, setTooBig] = useState(false);

  const upload = useMutation({
    ...uploadLabReportMutation(),
    onSuccess: () => {
      toast.success(`Result filed for ${patient.fullName}.`);
      setTestName('');
      setSummary('');
      setFile(null);
      void queryClient.invalidateQueries();
    },
  });

  const ready = testName.trim().length >= 2 && file !== null && !tooBig;

  return (
    <div className="card">
      <h2>File a result for {patient.fullName}</h2>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();

          if (!file) {
            return;
          }

          const written = summary.trim();

          upload.mutate({
            body: {
              PatientId: patient.id,
              TestName: testName.trim(),
              ...(written.length > 0 ? { Summary: written } : {}),
              File: file,
            },
          });
        }}
      >
        <label htmlFor="lab-test-name">What was tested</label>
        <input
          id="lab-test-name"
          value={testName}
          placeholder="Full blood count"
          maxLength={120}
          onChange={(event) => setTestName(event.target.value)}
        />

        <label htmlFor="lab-summary">Summary (optional)</label>
        <textarea
          id="lab-summary"
          rows={3}
          value={summary}
          maxLength={1000}
          placeholder="Haemoglobin low at 9.1 g/dL. Everything else within range."
          onChange={(event) => setSummary(event.target.value)}
        />

        <label htmlFor="lab-file">The report (PDF or photo, up to 10 MB)</label>
        <input
          id="lab-file"
          type="file"
          accept={ACCEPTED}
          onChange={(event: ChangeEvent<HTMLInputElement>) => {
            const chosen = event.target.files?.[0] ?? null;
            setFile(chosen);
            setTooBig(chosen !== null && chosen.size > MAX_BYTES);
          }}
        />

        {tooBig && (
          <p className="empty">
            That file is larger than 10 MB. Scan it again at a lower resolution.
          </p>
        )}

        <div className="actions">
          <button type="submit" disabled={upload.isPending || !ready}>
            {upload.isPending ? 'Uploading…' : 'File the result'}
          </button>
        </div>
      </form>
    </div>
  );
}

function kilobytes(bytes: number): string {
  return bytes < 1024 ? `${bytes} B` : `${Math.round(bytes / 1024)} kB`;
}
