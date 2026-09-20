import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  createMaintenanceScheduleMutation,
  listEquipmentItemsOptions,
  listMaintenanceSchedulesOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { MaintenanceSchedule, MaintenanceType } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canRunMaintenance } from '../types/permissions';
import {
  maintenanceStatusLabels,
  maintenanceTypeLabels,
  schedulableMaintenanceTypes,
} from '../types/maintenance';
import { ConfirmMaintenanceCard } from './equipment/ConfirmMaintenanceCard';
import { RetireItemDialog } from './equipment/RetireItemDialog';

const PAGE_SIZE = 10;

export function MaintenanceUnitPage() {
  const queryClient = useQueryClient();
  const session = useSession();
  const role = session?.principal.role;
  const manage = canRunMaintenance(role);

  const [page, setPage] = useState(1);
  const [scrapping, setScrapping] = useState<MaintenanceSchedule | null>(null);

  const queue = useQuery({
    ...listMaintenanceSchedulesOptions({
      query: { status: 'scheduled', page, pageSize: PAGE_SIZE },
    }),
    enabled: manage,
  });

  const rows = queue.data?.items ?? [];
  const totalPages = queue.data?.total_pages ?? 1;

  if (!manage) {
    return (
      <>
        <h1>Maintenance unit</h1>
        <p className="empty">
          The maintenance unit is run by the hospital administrator. To send a machine for repair,
          report a fault on it from the Equipment page.
        </p>
      </>
    );
  }

  return (
    <>
      <h1>Maintenance unit</h1>
      <p className="muted">
        Every service, calibration and repair still open. Confirm a job done once the work is
        finished, here or in the mobile app: that puts the item back into service and closes any
        fault reported against it.
      </p>

      <ConfirmMaintenanceCard />

      <ScheduleMaintenanceCard />

      <div className="card">
        <h2>Open jobs</h2>

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
          <p className="empty">Nothing waiting. Every job has been dealt with.</p>
        )}

        {rows.length > 0 && (
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Why</th>
                <th>Due</th>
                <th>Notes</th>
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
                    {job.asset_type === 'equipment_item' && job.schedule_type === 'repair' ? (
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => setScrapping(job)}
                      >
                        Beyond repair
                      </button>
                    ) : null}
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

      {scrapping && (
        <RetireItemDialog
          itemId={scrapping.asset_id}
          itemName={scrapping.asset_label}
          reason="Use this when the item cannot be fixed."
          onClose={() => setScrapping(null)}
          onDone={() => {
            setScrapping(null);
            queryClient.invalidateQueries();
          }}
        />
      )}
    </>
  );
}

function ScheduleMaintenanceCard() {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [assetId, setAssetId] = useState('');
  const [scheduleType, setScheduleType] = useState<MaintenanceType>('routine_service');
  const [scheduledDate, setScheduledDate] = useState('');
  const [notes, setNotes] = useState('');

  const items = useQuery(
    listEquipmentItemsOptions({
      query: {
        pageSize: 50,
        sortBy: 'name',
        sortDir: 'asc',
        ...(search.trim() ? { search: search.trim() } : {}),
      },
    }),
  );

  const choices = (items.data?.items ?? []).filter((item) => item.status !== 'retired');

  const schedule = useMutation({
    ...createMaintenanceScheduleMutation(),
    onSuccess: (job) => {
      toast.success(`${maintenanceTypeLabels[job.schedule_type]} booked for ${job.asset_label}.`);
      setAssetId('');
      setNotes('');
      queryClient.invalidateQueries();
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const written = notes.trim();
    schedule.mutate({
      body: {
        asset_type: 'equipment_item',
        asset_id: assetId,
        schedule_type: scheduleType,
        scheduled_date: scheduledDate,
        notes: written.length > 0 ? written : null,
      },
    });
  }

  return (
    <div className="card">
      <h2>Schedule maintenance</h2>
      <p className="muted" style={{ marginBottom: '0.9rem' }}>
        Book a service, calibration or repair for an equipment item. It joins the list below.
      </p>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="maintenance-search">Find an item</label>
            <input
              id="maintenance-search"
              value={search}
              placeholder="Name, model, tag or serial"
              onChange={(event) => {
                setSearch(event.target.value);
                setAssetId('');
              }}
            />
          </div>
          <div className="field">
            <label htmlFor="maintenance-item">Item</label>
            <select
              id="maintenance-item"
              value={assetId}
              onChange={(event) => setAssetId(event.target.value)}
              required
            >
              <option value="">{items.isPending ? 'Loading…' : 'Choose…'}</option>
              {choices.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name} ({item.asset_tag})
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="row">
          <div className="field">
            <label htmlFor="maintenance-type">Type</label>
            <select
              id="maintenance-type"
              value={scheduleType}
              onChange={(event) => setScheduleType(event.target.value as MaintenanceType)}
            >
              {schedulableMaintenanceTypes.map((type) => (
                <option key={type} value={type}>
                  {maintenanceTypeLabels[type]}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="maintenance-date">Date</label>
            <input
              id="maintenance-date"
              type="date"
              value={scheduledDate}
              onChange={(event) => setScheduledDate(event.target.value)}
              required
            />
          </div>
        </div>

        <label htmlFor="maintenance-notes">Notes (optional)</label>
        <textarea
          id="maintenance-notes"
          rows={2}
          maxLength={1000}
          value={notes}
          placeholder="Six-month service due."
          onChange={(event) => setNotes(event.target.value)}
        />

        <div className="actions">
          <button
            type="submit"
            disabled={schedule.isPending || assetId.length === 0 || scheduledDate.length === 0}
          >
            {schedule.isPending ? 'Booking…' : 'Schedule maintenance'}
          </button>
        </div>
      </form>
    </div>
  );
}
