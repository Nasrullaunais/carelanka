import { useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createPrescriptionMutation,
  listPatientsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PatientSummary } from '../services/api/generated';
import { Table } from '../components/Table';
import { useSession } from '../services/auth/useSession';
import { canWritePrescription } from '../types/permissions';
import { genderLabels } from '../types/patients';

const PAGE_SIZE = 10;
const MAX_BODY_LENGTH = 4000;
const MAX_FILE_BYTES = 10 * 1024 * 1024;
const ACCEPTED_FILE_TYPES = 'application/pdf,image/jpeg,image/png';

export function PrescriptionsPage() {
  const session = useSession();
  const allowed = canWritePrescription(session?.principal.role);

  const [selected, setSelected] = useState<PatientSummary | null>(null);

  if (!allowed) {
    return (
      <>
        <h1>Prescriptions</h1>
        <p className="empty">Writing a prescription is the doctor's.</p>
      </>
    );
  }

  return (
    <>
      <h1>Prescriptions</h1>
      <p className="muted">
        Look up a patient, then write a prescription straight to the pharmacy queue — the same
        queue a patient's own photographed prescription lands in.
      </p>

      <PatientSearch
        selected={selected}
        onSelect={setSelected}
        onCleared={() => setSelected(null)}
      />

      {selected && <WriteCard patient={selected} onSent={() => setSelected(null)} />}
    </>
  );
}

function PatientSearch({
  selected,
  onSelect,
  onCleared,
}: {
  selected: PatientSummary | null;
  onSelect: (patient: PatientSummary) => void;
  onCleared: () => void;
}) {
  const [search, setSearch] = useState('');

  const patients = useQuery({
    ...listPatientsOptions({ query: { search: search.trim(), pageSize: PAGE_SIZE } }),
    enabled: search.trim().length >= 2,
  });

  return (
    <div className="table-section">
      <h2>Find the patient</h2>

      <label htmlFor="prescription-search">Patient code, name or NIC</label>
      <input
        id="prescription-search"
        value={search}
        placeholder="P7K2X9QM or a name"
        onChange={(event) => {
          setSearch(event.target.value);
          onCleared();
        }}
      />

      {patients.isPending && search.trim().length >= 2 && <p className="muted">Searching…</p>}

      {patients.isError && (
        <p className="empty">
          Could not search patients.{' '}
          <button type="button" className="secondary" onClick={() => void patients.refetch()}>
            Try again
          </button>
        </p>
      )}

      {patients.isSuccess && patients.data.items.length === 0 && (
        <p className="empty">Nobody matches that.</p>
      )}

      {patients.isSuccess && patients.data.items.length > 0 && (
        <Table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Gender</th>
              <th>Date of birth</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {patients.data.items.map((patient) => (
              <tr key={patient.id}>
                <td>{patient.patient_code}</td>
                <td>{patient.full_name}</td>
                <td>{genderLabels[patient.gender]}</td>
                <td>
                  {patient.date_of_birth ?? <span className="muted">Not recorded</span>}
                </td>
                <td>
                  <button type="button" onClick={() => onSelect(patient)}>
                    {selected?.id === patient.id ? 'Selected' : 'Select'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </div>
  );
}

function WriteCard({ patient, onSent }: { patient: PatientSummary; onSent: () => void }) {
  const [mode, setMode] = useState<'type' | 'file'>('type');
  const [body, setBody] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [tooBig, setTooBig] = useState(false);

  const create = useMutation({
    ...createPrescriptionMutation(),
    onSuccess: () => {
      toast.success(`Sent to the pharmacy for ${patient.full_name}.`);
      setBody('');
      setFile(null);
      onSent();
    },
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (mode === 'type') {
      const trimmed = body.trim();
      if (trimmed.length === 0) {
        return;
      }
      create.mutate({ body: { PatientId: patient.id, Body: trimmed } });
    } else {
      if (!file) {
        return;
      }
      create.mutate({ body: { PatientId: patient.id, File: file } });
    }
  }

  const ready = mode === 'type' ? body.trim().length > 0 : file !== null && !tooBig;

  return (
    <div className="card">
      <h2>
        Write a prescription for {patient.full_name} ({patient.patient_code})
      </h2>
      <p className="muted">
        {genderLabels[patient.gender]}
        {patient.date_of_birth ? `, born ${patient.date_of_birth}` : ''}
        {patient.nic ? ` · NIC ${patient.nic}` : ''}
      </p>

      <div className="tabs">
        <button
          type="button"
          aria-pressed={mode === 'type'}
          onClick={() => {
            setMode('type');
            setFile(null);
            setTooBig(false);
          }}
        >
          Type it
        </button>
        <button
          type="button"
          aria-pressed={mode === 'file'}
          onClick={() => {
            setMode('file');
            setBody('');
          }}
        >
          Attach a photo or PDF
        </button>
      </div>

      <form onSubmit={handleSubmit}>
        {mode === 'type' ? (
          <>
            <label htmlFor="prescription-body">Medicine, dosage and instructions</label>
            <textarea
              id="prescription-body"
              rows={5}
              value={body}
              maxLength={MAX_BODY_LENGTH}
              placeholder="Amoxicillin 250mg, one three times a day for 5 days."
              onChange={(event) => setBody(event.target.value)}
            />
            <span className="hint">{body.length} / {MAX_BODY_LENGTH}</span>
          </>
        ) : (
          <>
            <label htmlFor="prescription-file">The prescription (PDF or photo, up to 10 MB)</label>
            <input
              id="prescription-file"
              type="file"
              accept={ACCEPTED_FILE_TYPES}
              onChange={(event: ChangeEvent<HTMLInputElement>) => {
                const chosen = event.target.files?.[0] ?? null;
                setFile(chosen);
                setTooBig(chosen !== null && chosen.size > MAX_FILE_BYTES);
              }}
            />
            {tooBig && (
              <p className="empty">
                That file is larger than 10 MB. Scan it again at a lower resolution.
              </p>
            )}
          </>
        )}

        <div className="actions">
          <button type="submit" disabled={create.isPending || !ready}>
            {create.isPending ? 'Sending…' : 'Send to the pharmacy'}
          </button>
        </div>
      </form>
    </div>
  );
}
