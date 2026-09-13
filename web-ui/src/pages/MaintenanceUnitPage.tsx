import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  completeMaintenanceScheduleMutation,
  listMaintenanceSchedulesOptions,
  updateEquipmentItemMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { MaintenanceSchedule } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canManageEquipment } from '../types/permissions';
import { maintenanceStatusLabels, maintenanceTypeLabels } from '../types/maintenance';
import { Dialog } from './EquipmentPage';

const PAGE_SIZE = 10;

export function MaintenanceUnitPage() {
  const session = useSession();
  const role = session?.principal.role;
  const manage = canManageEquipment(role);

  const [page, setPage] = useState(1);
  const [confirming, setConfirming] = useState<MaintenanceSchedule | null>(null);
  const [scrapping, setScrapping] = useState<MaintenanceSchedule | null>(null);

  const queue = useQuery(
    listMaintenanceSchedulesOptions({
      query: { status: 'scheduled', page, pageSize: PAGE_SIZE },
    }),
  );

  const rows = queue.data?.items ?? [];
  const totalPages = queue.data?.total_pages ?? 1;

  return (
    <>
      <h1>Maintenance unit</h1>
      <p className="muted">
        Everything the unit still has to fix or service. An item listed here is out of service,
        and it stays out until the repair is confirmed done.
      </p>

      {queue.isPending && <p className="empty">Loading the queue…</p>}

      {queue.isError && (
        <p className="empty">
          The queue could not be loaded.{' '}
          <button type="button" className="secondary" onClick={() => queue.refetch()}>
            Try again
          </button>
        </p>
      )}

      {queue.isSuccess && rows.length === 0 && (
        <p className="empty">Nothing waiting. Every reported fault has been dealt with.</p>
      )}

      {rows.length > 0 && (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Why</th>
                <th>Reported</th>
                <th>What was reported</th>
                <th>State</th>
                {manage && <th>Action</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((job) => (
                <tr key={job.id}>
                  <td>{job.asset_label}</td>
                  <td>{maintenanceTypeLabels[job.schedule_type]}</td>
                  <td>{job.scheduled_date}</td>
                  <td>{job.notes ?? <span className="muted">No description given.</span>}</td>
                  <td>
                    <span className="badge">{maintenanceStatusLabels[job.status]}</span>
                  </td>
                  {manage && (
                    <td>
                      <button type="button" onClick={() => setConfirming(job)}>
                        Confirm repaired
                      </button>{' '}

                      {job.asset_type === 'equipment_item' && (
                        <button
                          type="button"
                          className="secondary"
                          onClick={() => setScrapping(job)}
                        >
                          Beyond repair
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
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

      {confirming && (
        <ConfirmRepairDialog job={confirming} onClose={() => setConfirming(null)} />
      )}

      {scrapping && <ScrapDialog job={scrapping} onClose={() => setScrapping(null)} />}
    </>
  );
}

function ConfirmRepairDialog({
  job,
  onClose,
}: {
  job: MaintenanceSchedule;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [notes, setNotes] = useState('');

  const complete = useMutation({
    ...completeMaintenanceScheduleMutation(),
    onSuccess: () => {
      toast.success(`${job.asset_label} is back in service.`);
      queryClient.invalidateQueries();
      onClose();
    },
  });

  return (
    <Dialog title={`Confirm repair of ${job.asset_label}`} onClose={onClose}>
      <p className="muted">
        This records the work against you and puts the item back into service. It also closes
        the fault that was reported against it.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          const written = notes.trim();
          complete.mutate({
            path: { id: job.id },
            body: written.length > 0 ? { notes: written } : {},
          });
        }}
      >
        <label htmlFor="repair-notes">What was done (optional)</label>
        <textarea
          id="repair-notes"
          rows={3}
          value={notes}
          placeholder="Replaced the power lead."
          onChange={(event) => setNotes(event.target.value)}
        />

        <div className="actions">
          <button type="submit" disabled={complete.isPending}>
            {complete.isPending ? 'Confirming…' : 'Confirm repaired'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}

function ScrapDialog({ job, onClose }: { job: MaintenanceSchedule; onClose: () => void }) {
  const queryClient = useQueryClient();

  const retire = useMutation({
    ...updateEquipmentItemMutation(),
    onSuccess: () => {
      toast.success(`${job.asset_label} has been retired.`);
      queryClient.invalidateQueries();
      onClose();
    },
  });

  return (
    <Dialog title={`Retire ${job.asset_label}`} onClose={onClose}>
      <p className="muted">
        Use this when the item cannot be fixed. Retiring is permanent: a replacement is
        registered as a new item, never by bringing this one back. The open job and the
        reported fault are closed with it.
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={retire.isPending}
          onClick={() => retire.mutate({ path: { id: job.asset_id }, body: { status: 'retired' } })}
        >
          {retire.isPending ? 'Retiring…' : 'Retire it'}
        </button>
        <button type="button" className="secondary" onClick={onClose}>
          Cancel
        </button>
      </div>
    </Dialog>
  );
}
