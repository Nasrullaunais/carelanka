import { useState } from 'react';
import type { ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getEquipmentItemOptions,
  updateEquipmentItemMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type { EquipmentStatus } from '../../services/api/generated';
import { useSession } from '../../services/auth/useSession';
import { canManageEquipment } from '../../types/permissions';
import {
  editableTransitions,
  equipmentStatusLabels,
  warningSeverityLabels,
} from '../../types/equipment';
import { StatusBadge } from '../EquipmentPage';

export function ItemDetailCard({ id, onClose }: { id: string; onClose: () => void }) {
  const session = useSession();
  const queryClient = useQueryClient();
  const detail = useQuery(getEquipmentItemOptions({ path: { id } }));

  const [moveTo, setMoveTo] = useState<EquipmentStatus | ''>('');

  const update = useMutation({
    ...updateEquipmentItemMutation(),
    onSuccess: (item) => {
      toast.success(`${item.name} is now ${equipmentStatusLabels[item.status].toLowerCase()}.`);
      setMoveTo('');
      queryClient.invalidateQueries({
        predicate: (query) => {
          const key = (query.queryKey[0] as { _id?: string } | undefined)?._id;
          return key === 'listEquipmentItems' || key === 'getEquipmentItem';
        },
      });
    },
  });

  if (detail.isLoading) {
    return (
      <div className="card">
        <p className="empty">Loading item…</p>
      </div>
    );
  }

  if (detail.isError || !detail.data) {
    return (
      <div className="card">
        <div className="empty">
          <p>Could not load this item.</p>
          <button type="button" className="secondary" onClick={() => void detail.refetch()}>
            Try again
          </button>
        </div>
      </div>
    );
  }

  const item = detail.data;
  const moves = editableTransitions[item.status];

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>
          {item.name} <StatusBadge status={item.status} />
        </h2>
        <button type="button" className="secondary" onClick={onClose}>
          Close
        </button>
      </div>

      <dl className="detail-grid">
        <Field label="Asset tag" value={<code>{item.asset_tag}</code>} />
        <Field label="Serial number" value={item.serial_number ?? <Muted>Not recorded</Muted>} />
        <Field label="Category" value={item.category_name} />
        <Field label="Manufacturer" value={`${item.manufacturer} ${item.model}`} />
        <Field label="Where" value={item.ward_name ?? <Muted>Central store</Muted>} />
        <Field label="Purchased" value={item.purchase_date} />
        <Field
          label="Next service"
          value={item.next_maintenance_due ?? <Muted>None booked</Muted>}
        />
        <Field
          label="Assigned to"
          value={
            item.assigned_to_admission_id ? (
              <code>{item.assigned_to_admission_id}</code>
            ) : (
              <Muted>Nobody</Muted>
            )
          }
        />
      </dl>

      {canManageEquipment(session?.principal.role) && (
        <div className="row" style={{ marginTop: '1rem', alignItems: 'end' }}>
          <div className="field">
            <label htmlFor="move-to">Move to</label>
            <select
              id="move-to"
              value={moveTo}
              disabled={moves.length === 0}
              onChange={(event) => setMoveTo(event.target.value as EquipmentStatus | '')}
            >
              <option value="">
                {moves.length === 0 ? 'Retired is the end of the line' : 'Choose…'}
              </option>
              {moves.map((next) => (
                <option key={next} value={next}>
                  {equipmentStatusLabels[next]}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <button
              type="button"
              disabled={update.isPending || moveTo === ''}
              onClick={() =>
                update.mutate({ path: { id: item.id }, body: { status: moveTo as EquipmentStatus } })
              }
            >
              {update.isPending ? 'Moving…' : 'Apply'}
            </button>
          </div>
        </div>
      )}

      {/* Assignment is missing from this list on purpose. Available -> assigned is a legal
          move, but only the assign endpoint can make it, because it carries the admission
          id. The Assign button on the register row is that path. */}
      <p className="hint">
        Only moves the server will accept are offered here. Reporting a fault is the one
        exception to that list: it can move an item into maintenance from any state but
        retired, including while a patient is using it.
      </p>

      <h3>Open warnings</h3>
      {item.open_warnings.length === 0 ? (
        <p className="muted small">Nothing open against this item.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Severity</th>
              <th>Raised</th>
              <th>Recommended action</th>
            </tr>
          </thead>
          <tbody>
            {item.open_warnings.map((warning) => (
              <tr key={warning.id}>
                <td>
                  <span className={`badge severity-${warning.severity}`}>
                    {warningSeverityLabels[warning.severity]}
                  </span>
                </td>
                <td className="muted small">
                  {warning.raised_by === 'user' ? 'By a person' : 'By the agent'}
                </td>
                <td>{warning.recommended_action}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <h3>Maintenance history</h3>
      {item.maintenance_history.length === 0 ? (
        <p className="muted small">No servicing recorded yet.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Scheduled</th>
              <th>Type</th>
              <th>Status</th>
              <th>Notes</th>
            </tr>
          </thead>
          <tbody>
            {item.maintenance_history.map((entry) => (
              <tr key={entry.id}>
                <td>{entry.scheduled_date}</td>
                <td>{entry.schedule_type.replace('_', ' ')}</td>
                <td>{entry.status.replace('_', ' ')}</td>
                <td>{entry.notes ?? <Muted>None</Muted>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function Muted({ children }: { children: ReactNode }) {
  return <span className="muted">{children}</span>;
}
