import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  getPharmacyItemOptions,
  listPharmacyTransactionsOptions,
} from '../../services/api/generated/@tanstack/react-query.gen';
import { useSession } from '../../services/auth/useSession';
import { canManageEquipment } from '../../types/permissions';
import { takesStock, transactionTypeLabels } from '../../types/pharmacy';

export function ItemHistoryCard({ id, onClose }: { id: string; onClose: () => void }) {
  const session = useSession();
  const mayReadHistory = canManageEquipment(session?.principal.role);

  const item = useQuery(getPharmacyItemOptions({ path: { id } }));

  // The history endpoint is equipment-only, so a nurse who opened this card would otherwise
  // trigger a 403 toast just by clicking a name. Hidden rather than attempted.
  const history = useQuery({
    ...listPharmacyTransactionsOptions({ path: { id }, query: { pageSize: 20 } }),
    enabled: mayReadHistory,
  });

  if (item.isLoading) {
    return (
      <div className="card">
        <p className="empty">Loading item…</p>
      </div>
    );
  }

  if (item.isError || !item.data) {
    return (
      <div className="card">
        <div className="empty">
          <p>Could not load this item.</p>
          <button type="button" className="secondary" onClick={() => void item.refetch()}>
            Try again
          </button>
        </div>
      </div>
    );
  }

  const it = item.data;
  const rows = history.data?.items ?? [];

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>
          {it.name}{' '}
          <span className="badge status-available">
            {it.quantity_on_hand} {it.unit}
          </span>
        </h2>
        <button type="button" className="secondary" onClick={onClose}>
          Close
        </button>
      </div>

      <dl className="detail-grid">
        <Field label="Category" value={it.category_name} />
        <Field label="Manufacturer" value={it.manufacturer ?? <Muted>Not recorded</Muted>} />
        <Field label="Batch" value={it.batch_number ?? <Muted>Not tracked</Muted>} />
        <Field label="Expires" value={it.expiry_date ?? <Muted>Does not expire</Muted>} />
        <Field label="Reorder at" value={`${it.reorder_threshold} ${it.unit}`} />
        <Field
          label="Unit price"
          value={it.unit_price == null ? <Muted>Not recorded</Muted> : it.unit_price.toFixed(2)}
        />
      </dl>

      <h3>Movement history</h3>

      {!mayReadHistory ? (
        <p className="muted small">
          The movement history is open to equipment staff only. Anyone may search the
          pharmacy and see what is on the shelf.
        </p>
      ) : history.isLoading ? (
        <p className="empty">Loading…</p>
      ) : rows.length === 0 ? (
        <p className="muted small">
          Nothing has moved since this item was added. The quantity shown is its opening
          stock.
        </p>
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>When</th>
                <th>What happened</th>
                <th>Change</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td className="muted small">
                    {new Date(row.created_at).toLocaleString()}
                  </td>
                  <td>{transactionTypeLabels[row.type]}</td>
                  <td>
                    {/* Quantity is always positive on the wire; the type is what gives it a
                        sign. Showing it unsigned would make a delivery and a dispense look
                        identical in this column. */}
                    <strong>
                      {takesStock[row.type] ? '−' : '+'}
                      {row.quantity}
                    </strong>{' '}
                    <span className="muted small">{it.unit}</span>
                  </td>
                  <td>{row.note ?? <Muted>None</Muted>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="hint">
            This is the audit trail, so nothing here is ever edited or deleted. It is also
            what the consumption report and the low-stock sweep will read.
          </p>
        </>
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
