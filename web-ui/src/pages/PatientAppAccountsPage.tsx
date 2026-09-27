import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  listPatientAppAccountsOptions,
  resetPatientAppPasswordMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { PatientAppAccount } from '../services/api/generated';
import { ConfirmDialog } from '../components/ui/confirm-dialog';
import { PaginationControls } from '../components/ui/pagination-controls';
import { Table } from '../components/Table';

const pageSize = 20;

export function PatientAppAccountsPage() {
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [page, setPage] = useState(1);
  const [target, setTarget] = useState<PatientAppAccount | null>(null);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timeout);
  }, [search]);

  const accounts = useQuery(
    listPatientAppAccountsOptions({
      query: { page, pageSize, ...(debouncedSearch ? { search: debouncedSearch } : {}) },
    }),
  );

  // gcTime 0 and reset() on close: the temporary password must not outlive the dialog.
  const reset = useMutation({ ...resetPatientAppPasswordMutation(), gcTime: 0 });

  function close() {
    setTarget(null);
    reset.reset();
  }

  const rows = accounts.data?.items ?? [];

  return (
    <>
      <h1>Patient app accounts</h1>
      <p className="muted">
        A patient who has forgotten their app password calls or comes to the desk. Find them here,
        check their NIC and date of birth against the person, and give them a new password.
      </p>

      <div className="card">
        <div className="field">
          <label htmlFor="patient-account-search">Search</label>
          <input
            id="patient-account-search"
            value={search}
            maxLength={100}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Name, NIC, patient code or app username"
          />
        </div>
      </div>

      <div className="table-section">
        <h2>Patients with an app login</h2>

        {accounts.isError ? (
          <div className="empty">
            <p>Could not load patient app accounts.</p>
            <button type="button" className="secondary" onClick={() => void accounts.refetch()}>
              Try again
            </button>
          </div>
        ) : accounts.isLoading ? (
          <p className="empty">Loading…</p>
        ) : rows.length === 0 ? (
          <p className="empty">
            {debouncedSearch
              ? 'No patient with an app login matches that search.'
              : 'No patient has an app login linked to their record yet.'}
          </p>
        ) : (
          <Table
            footer={
              accounts.data && (
                <PaginationControls
                  label="Patient app accounts"
                  page={page}
                  totalPages={accounts.data.total_pages}
                  totalItems={accounts.data.total_items}
                  onPageChange={setPage}
                />
              )
            }
          >
            <thead>
              <tr>
                <th>Name</th>
                <th>NIC</th>
                <th>Date of birth</th>
                <th>Patient code</th>
                <th>App username</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.patient_id}>
                  <td>{row.full_name}</td>
                  <td>{row.nic ?? <span className="muted">Not recorded</span>}</td>
                  <td>{row.date_of_birth ?? <span className="muted">Not recorded</span>}</td>
                  <td>{row.patient_code}</td>
                  <td>{row.username}</td>
                  <td>
                    <button type="button" className="secondary" onClick={() => setTarget(row)}>
                      Generate new password
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </div>

      <ConfirmDialog
        isOpen={target !== null}
        onOpenChange={(open) => {
          if (!open) close();
        }}
        title={reset.data ? 'New password' : 'Generate new password'}
        tone={reset.data ? 'accent' : 'danger'}
        confirmLabel={reset.data ? 'Done' : 'Generate new password'}
        cancelLabel={reset.data ? 'Close' : 'Cancel'}
        isPending={reset.isPending}
        onConfirm={() => {
          if (reset.data) {
            close();
          } else if (target) {
            reset.mutate({ path: { patientId: target.patient_id } });
          }
        }}
        description={
          reset.data ? (
            <TemporaryPassword username={reset.data.username} password={reset.data.temporary_password} />
          ) : (
            target && (
              <p>
                Generate a new password for {target.full_name}? Their current password stops
                working straight away. Check their NIC and date of birth first.
              </p>
            )
          )
        }
      />
    </>
  );
}

function TemporaryPassword({ username, password }: { username: string; password: string }) {
  async function copy() {
    try {
      await navigator.clipboard.writeText(password);
      toast.success('Copied.');
    } catch {
      toast.error('Could not copy. Read the password out instead.');
    }
  }

  return (
    <div>
      <p className="muted small">App username</p>
      <p style={{ fontFamily: 'monospace', fontSize: '1.25rem' }}>{username}</p>
      <p className="muted small" style={{ marginTop: '0.8rem' }}>Temporary password</p>
      <p aria-label="Temporary password" style={{ fontFamily: 'monospace', fontSize: '2rem', letterSpacing: '0.1em' }}>
        {password}
      </p>
      <button type="button" className="secondary" onClick={() => void copy()}>
        Copy
      </button>
      <p style={{ marginTop: '0.8rem' }}>
        <strong>This is shown once. Give it to the patient now.</strong> They will be asked to
        choose their own password when they sign in.
      </p>
    </div>
  );
}
