import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  confirmMaintenanceScheduleMutation,
  listMaintenanceSchedulesAwaitingConfirmationOptions,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type { MaintenanceSchedule } from '../../services/api/generated';
import { maintenanceStatusLabels, maintenanceTypeLabels } from '../../types/maintenance';
import { Dialog } from '../EquipmentPage';

const CODE_HEADER = 'X-Confirmation-Code';

// The hospital administrator's maintenance queue, the same one the mobile app shows. The code is
// kept in this component only - never stored - and the API checks it on every call.
export function ConfirmMaintenanceCard() {
  const queryClient = useQueryClient();
  const [code, setCode] = useState<string | null>(null);
  const [entered, setEntered] = useState('');
  const [unlocking, setUnlocking] = useState(false);
  const [confirming, setConfirming] = useState<MaintenanceSchedule | null>(null);

  const open = useQuery({
    ...listMaintenanceSchedulesAwaitingConfirmationOptions({
      headers: { [CODE_HEADER]: code ?? '' },
    }),
    enabled: code !== null,
  });

  async function unlock(event: FormEvent) {
    event.preventDefault();
    const tried = entered.trim();
    if (tried.length === 0) return;

    setUnlocking(true);

    try {
      // Asked once before unlocking, so a wrong code leaves the card locked. The failure itself
      // is toasted by the transport with the server's message.
      await queryClient.fetchQuery(
        listMaintenanceSchedulesAwaitingConfirmationOptions({ headers: { [CODE_HEADER]: tried } }),
      );
      setCode(tried);
    } catch {
      setEntered('');
    } finally {
      setUnlocking(false);
    }
  }

  function lock() {
    setCode(null);
    setEntered('');
    queryClient.removeQueries({
      predicate: (query) =>
        (query.queryKey[0] as { _id?: string } | undefined)?._id ===
        'listMaintenanceSchedulesAwaitingConfirmation',
    });
  }

  if (code === null) {
    return (
      <div className="card">
        <h2>Confirm maintenance done</h2>
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          Reported faults and scheduled maintenance wait here until you confirm the work is done.
          Enter the confirmation code to see them.
        </p>

        <form onSubmit={unlock}>
          <div className="row">
            <div className="field">
              <label htmlFor="maintenance-confirmation-code">Confirmation code</label>
              <input
                id="maintenance-confirmation-code"
                type="password"
                autoComplete="off"
                value={entered}
                disabled={unlocking}
                onChange={(event) => setEntered(event.target.value)}
              />
            </div>
            <div className="field" style={{ alignSelf: 'end' }}>
              <button type="submit" disabled={unlocking || entered.trim().length === 0}>
                {unlocking ? 'Checking…' : 'Unlock'}
              </button>
            </div>
          </div>
        </form>
      </div>
    );
  }

  const rows = open.data ?? [];

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>Confirm maintenance done</h2>
        <button type="button" className="secondary" onClick={lock}>
          Lock
        </button>
      </div>

      {open.isPending && <p className="empty">Loading…</p>}

      {open.isError && (
        <p className="empty">
          The list could not be loaded.{' '}
          <button type="button" className="secondary" onClick={lock}>
            Enter the code again
          </button>
        </p>
      )}

      {open.isSuccess && rows.length === 0 && (
        <p className="empty">Nothing is open. Every fault and service has been confirmed.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Item</th>
              <th>Why</th>
              <th>Due</th>
              <th>What was reported</th>
              <th>State</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((job) => (
              <tr key={job.id}>
                <td>{job.asset_label}</td>
                <td>{maintenanceTypeLabels[job.schedule_type]}</td>
                <td>{job.scheduled_date}</td>
                <td>{job.notes ?? <span className="muted">No notes.</span>}</td>
                <td>
                  <span className="badge">{maintenanceStatusLabels[job.status]}</span>
                </td>
                <td>
                  <button type="button" onClick={() => setConfirming(job)}>
                    Confirm done
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {confirming && (
        <ConfirmDoneDialog job={confirming} code={code} onClose={() => setConfirming(null)} />
      )}
    </div>
  );
}

function ConfirmDoneDialog({
  job,
  code,
  onClose,
}: {
  job: MaintenanceSchedule;
  code: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();

  const confirm = useMutation({
    ...confirmMaintenanceScheduleMutation(),
    onSuccess: () => {
      toast.success(`${job.asset_label} is confirmed repaired and back in service.`);
      queryClient.invalidateQueries();
      onClose();
    },
  });

  return (
    <Dialog title={`Confirm ${job.asset_label} is done?`} onClose={onClose}>
      <p className="muted">
        The item goes back into service, and any fault reported against it is closed.
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={confirm.isPending}
          onClick={() => confirm.mutate({ path: { id: job.id }, headers: { [CODE_HEADER]: code } })}
        >
          {confirm.isPending ? 'Confirming…' : 'Confirm done'}
        </button>
        <button type="button" className="secondary" onClick={onClose}>
          Cancel
        </button>
      </div>
    </Dialog>
  );
}
