import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { recordPharmacyTransactionMutation } from '../../services/api/generated/@tanstack/react-query.gen';
import type { PharmacyItem, PharmacyTransactionType } from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';
import {
  needsNote,
  takesStock,
  transactionTypeLabels,
  transactionTypes,
} from '../../types/pharmacy';

export function StockDialog({
  item,
  onClose,
  onChanged,
}: {
  item: PharmacyItem;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [type, setType] = useState<PharmacyTransactionType>('dispensed');
  const [quantity, setQuantity] = useState('1');
  const [note, setNote] = useState('');

  const record = useMutation({
    ...recordPharmacyTransactionMutation(),
    onSuccess: (updated) => {
      toast.success(
        `${updated.name} now reads ${updated.quantity_on_hand} ${updated.unit}.`,
      );
      onChanged();
      onClose();
    },
  });

  const amount = Number(quantity);
  const takes = takesStock[type];
  const noteRequired = needsNote[type];

  // Shown before the request, but it is not the check. The server decides with one
  // conditional update, which is also what stops two people taking the last box at once.
  const wouldOverdraw = takes && amount > item.quantity_on_hand;

  const after = takes
    ? item.quantity_on_hand - amount
    : item.quantity_on_hand + amount;

  function submit(event: FormEvent) {
    event.preventDefault();
    record.mutate({
      path: { id: item.id },
      body: { type, quantity: amount, note: note.trim() || null },
    });
  }

  return (
    <Dialog title={`Record a movement for ${item.name}`} onClose={onClose}>
      <p className="muted">
        The quantity is never edited directly. Every change is a movement, so the shelf and
        the history can never tell different stories.
      </p>

      <form onSubmit={submit}>
        <div className="row">
          <div className="field">
            <label htmlFor="mv-type">What happened</label>
            <select
              id="mv-type"
              value={type}
              onChange={(event) => setType(event.target.value as PharmacyTransactionType)}
            >
              {transactionTypes.map((value) => (
                <option key={value} value={value}>
                  {transactionTypeLabels[value]}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="mv-qty">How many {item.unit}</label>
            <input
              id="mv-qty"
              type="number"
              min={1}
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
              required
            />
          </div>
        </div>

        <div className="field">
          <label htmlFor="mv-note">
            Note {noteRequired ? '(required for an adjustment)' : '(optional)'}
          </label>
          <textarea
            id="mv-note"
            rows={2}
            maxLength={300}
            value={note}
            placeholder={
              noteRequired ? 'Stocktake found two fewer boxes than recorded.' : ''
            }
            onChange={(event) => setNote(event.target.value)}
            required={noteRequired}
          />
          {noteRequired && (
            <p className="hint">
              An adjustment is the one movement with no delivery or prescription behind it.
              Without a note nobody can audit it afterwards, so the server refuses it.
            </p>
          )}
        </div>

        <p className={wouldOverdraw ? 'stub-note' : 'hint'}>
          {wouldOverdraw ? (
            <>
              <strong>Only {item.quantity_on_hand} {item.unit} on hand.</strong> The server
              will refuse this and nothing will move.
            </>
          ) : (
            <>
              {item.quantity_on_hand} {item.unit} now, {after} after this movement.
            </>
          )}
        </p>

        <div className="actions">
          <button
            type="submit"
            disabled={
              record.isPending ||
              amount < 1 ||
              (noteRequired && note.trim().length === 0)
            }
          >
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
