import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  addPharmacyBatchMutation,
  listPharmacyBatchesOptions,
  recordPharmacyBatchTransactionMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type {
  PharmacyBatch,
  PharmacyItem,
  PharmacyTransactionType,
} from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';
import {
  movementTypes,
  needsNote,
  takesStock,
  transactionTypeLabels,
} from '../../types/pharmacy';

// Every delivery of one medicine, newest last. Stock is dispensed from whichever batch expires
// first, so the order here is also the order it leaves the shelf.
export function BatchList({
  item,
  manage,
  onChanged,
}: {
  item: PharmacyItem;
  manage: boolean;
  onChanged: () => void;
}) {
  const batches = useQuery(listPharmacyBatchesOptions({ path: { id: item.id } }));
  const [moving, setMoving] = useState<PharmacyBatch | null>(null);
  const rows = batches.data ?? [];

  if (batches.isPending) {
    return <p className="muted small">Loading batches…</p>;
  }

  if (batches.isError) {
    return (
      <p className="muted small">
        The batches could not be loaded.{' '}
        <button type="button" className="linklike" onClick={() => batches.refetch()}>
          Try again
        </button>
      </p>
    );
  }

  if (rows.length === 0) {
    return <p className="muted small">No batches yet. Add one when a delivery arrives.</p>;
  }

  return (
    <>
      <table>
        <thead>
          <tr>
            <th>Batch</th>
            <th>Expires</th>
            <th>On hand</th>
            <th>Batch code</th>
            <th>Received</th>
            {manage && <th>Movement</th>}
          </tr>
        </thead>
        <tbody>
          {rows.map((batch) => (
            <tr key={batch.id}>
              <td>
                <strong>{ordinal(batch.batch_number)} batch</strong>
              </td>
              <td>{batch.expiry_date ?? <span className="muted">Does not expire</span>}</td>
              <td>
                {batch.quantity_on_hand === 0 ? (
                  <span className="muted">Used up</span>
                ) : (
                  <>
                    <strong>{batch.quantity_on_hand}</strong>{' '}
                    <span className="muted small">{item.unit}</span>
                  </>
                )}
              </td>
              <td>{batch.reference ?? <span className="muted">None</span>}</td>
              <td className="muted small">{new Date(batch.received_at).toLocaleDateString()}</td>
              {manage && (
                <td>
                  <button
                    type="button"
                    className="secondary"
                    disabled={batch.quantity_on_hand === 0}
                    onClick={() => setMoving(batch)}
                  >
                    Record movement
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
        </table>

      {moving && (
        <BatchMovementDialog
          item={item}
          batch={moving}
          onClose={() => setMoving(null)}
          onChanged={onChanged}
        />
      )}
    </>
  );
}

// A movement against one named batch, for when the pharmacist is holding that box: expired stock
// off one delivery, or a stocktake correction on it. Dispensing from the item instead takes from
// whichever batch expires first.
function BatchMovementDialog({
  item,
  batch,
  onClose,
  onChanged,
}: {
  item: PharmacyItem;
  batch: PharmacyBatch;
  onClose: () => void;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const [type, setType] = useState<PharmacyTransactionType>('dispensed');
  const [quantity, setQuantity] = useState('1');
  const [note, setNote] = useState('');

  const record = useMutation({
    ...recordPharmacyBatchTransactionMutation(),
    onSuccess: (updated) => {
      toast.success(`${updated.name} now reads ${updated.quantity_on_hand} ${updated.unit}.`);
      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPharmacyBatches',
      });
      onChanged();
      onClose();
    },
  });

  const amount = Number(quantity);
  const takes = takesStock[type];
  const noteRequired = needsNote[type];
  const wouldOverdraw = takes && amount > batch.quantity_on_hand;

  return (
    <Dialog
      title={`${ordinal(batch.batch_number)} batch of ${item.name}`}
      onClose={onClose}
    >
      <p className="muted">
        This movement comes out of this batch only. It holds {batch.quantity_on_hand} {item.unit}
        {batch.expiry_date ? `, expiring ${batch.expiry_date}` : ''}.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          record.mutate({
            path: { id: item.id, batchId: batch.id },
            body: { type, quantity: amount, note: note.trim() || null },
          });
        }}
      >
        <div className="row">
          <div className="field">
            <label htmlFor="batch-mv-type">What happened</label>
            <select
              id="batch-mv-type"
              value={type}
              onChange={(event) => setType(event.target.value as PharmacyTransactionType)}
            >
              {movementTypes.map((value) => (
                <option key={value} value={value}>
                  {transactionTypeLabels[value]}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="batch-mv-qty">How many {item.unit}</label>
            <input
              id="batch-mv-qty"
              type="number"
              min={1}
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
              required
            />
          </div>
        </div>

        <div className="field">
          <label htmlFor="batch-mv-note">
            Note {noteRequired ? '(required for an adjustment)' : '(optional)'}
          </label>
          <textarea
            id="batch-mv-note"
            rows={2}
            maxLength={300}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            required={noteRequired}
          />
        </div>

        {wouldOverdraw && (
          <p className="field-error">
            This batch only holds {batch.quantity_on_hand} {item.unit}.
          </p>
        )}

        <div className="actions">
          <button type="submit" disabled={record.isPending || amount < 1 || wouldOverdraw}>
            {record.isPending ? 'Recording…' : 'Record movement'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}

export function AddBatchDialog({
  item,
  onClose,
  onChanged,
}: {
  item: PharmacyItem;
  onClose: () => void;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const [quantity, setQuantity] = useState('1');
  const [expiryDate, setExpiryDate] = useState('');
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');

  const add = useMutation({
    ...addPharmacyBatchMutation(),
    onSuccess: (batch) => {
      toast.success(
        `${ordinal(batch.batch_number)} batch of ${item.name} added: ${batch.quantity_on_hand} ${item.unit}.`,
      );
      queryClient.invalidateQueries({
        predicate: (query) =>
          (query.queryKey[0] as { _id?: string } | undefined)?._id === 'listPharmacyBatches',
      });
      onChanged();
      onClose();
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    add.mutate({
      path: { id: item.id },
      body: {
        quantity: Number(quantity),
        expiry_date: expiryDate || null,
        reference: reference.trim() || null,
        note: note.trim() || null,
      },
    });
  }

  return (
    <Dialog title={`Add a batch of ${item.name}`} onClose={onClose}>
      <p className="muted">
        A delivery of a medicine already in the catalog. It becomes the next batch, with its own
        expiry date, and is dispensed after any batch that expires sooner.
      </p>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="batch-qty">How many {item.unit} arrived</label>
            <input
              id="batch-qty"
              type="number"
              min={1}
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="batch-expiry">Expiry date</label>
            <input
              id="batch-expiry"
              type="date"
              value={expiryDate}
              onChange={(event) => setExpiryDate(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="batch-ref">Batch code on the box (optional)</label>
            <input
              id="batch-ref"
              value={reference}
              maxLength={50}
              onChange={(event) => setReference(event.target.value)}
            />
          </div>
        </div>

        <div className="field">
          <label htmlFor="batch-note">Note (optional)</label>
          <textarea
            id="batch-note"
            rows={2}
            maxLength={300}
            value={note}
            placeholder="Delivered by the supplier on Monday."
            onChange={(event) => setNote(event.target.value)}
          />
        </div>

        <div className="actions">
          <button type="submit" disabled={add.isPending || Number(quantity) < 1}>
            {add.isPending ? 'Adding…' : 'Add batch'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}

export function ordinal(value: number): string {
  const rest = value % 100;

  if (rest >= 11 && rest <= 13) {
    return `${value}th`;
  }

  return `${value}${['th', 'st', 'nd', 'rd'][value % 10] ?? 'th'}`;
}
