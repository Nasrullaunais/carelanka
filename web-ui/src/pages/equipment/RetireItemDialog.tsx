import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  removeEquipmentItemMutation,
  retireEquipmentItemMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import { Dialog } from '../EquipmentPage';

const CODE_HEADER = 'X-Confirmation-Code';

// Retiring is irreversible, so it asks for the confirmation code as well as being the hospital
// administrator's alone. The code is typed here and never stored; the API is what checks it.
export function RetireItemDialog({
  itemId,
  itemName,
  assetTag,
  reason,
  onClose,
  onDone,
}: {
  itemId: string;
  itemName: string;
  assetTag?: string;
  reason?: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const [code, setCode] = useState('');

  const retire = useMutation({
    ...retireEquipmentItemMutation(),
    onSuccess: (item) => {
      toast.success(`${item.name} is retired and off the register.`);
      onDone();
    },
    onError: () => setCode(''),
  });

  return (
    <Dialog title={`Retire ${itemName}?`} onClose={onClose}>
      <p className="muted">
        {reason ??
          'Use this when the item is broken beyond repair or no longer used in the hospital.'}{' '}
        Retiring is permanent: it leaves the register, cannot be assigned to anyone again, and a
        replacement is registered as a new item. Any open repair job and fault are closed with it.
        {assetTag ? ` Asset tag ${assetTag} stays with this record.` : ''}
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          retire.mutate({ path: { id: itemId }, headers: { [CODE_HEADER]: code.trim() } });
        }}
      >
        <label htmlFor="retire-code">Confirmation code</label>
        <input
          id="retire-code"
          type="password"
          autoComplete="off"
          value={code}
          disabled={retire.isPending}
          onChange={(event) => setCode(event.target.value)}
        />

        <div className="actions">
          <button type="submit" disabled={retire.isPending || code.trim().length === 0}>
            {retire.isPending ? 'Retiring…' : 'Retire it'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}

// Removing only tidies the register once an item is retired, so it asks for the same code as
// retiring did rather than being a quiet one-click delete.
export function RemoveItemDialog({
  itemId,
  itemName,
  assetTag,
  onClose,
  onDone,
}: {
  itemId: string;
  itemName: string;
  assetTag: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const [code, setCode] = useState('');

  const remove = useMutation({
    ...removeEquipmentItemMutation(),
    onSuccess: () => {
      toast.success(`${itemName} removed from the register.`);
      onDone();
    },
    onError: () => setCode(''),
  });

  return (
    <Dialog title={`Remove ${itemName} from the register?`} onClose={onClose}>
      <p className="muted">
        It disappears from the register and from every list on this page. Its history stays in the
        database for the record, and asset tag {assetTag} can be used again by a new item.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          remove.mutate({ path: { id: itemId }, headers: { [CODE_HEADER]: code.trim() } });
        }}
      >
        <label htmlFor="remove-code">Confirmation code</label>
        <input
          id="remove-code"
          type="password"
          autoComplete="off"
          value={code}
          disabled={remove.isPending}
          onChange={(event) => setCode(event.target.value)}
        />

        <div className="actions">
          <button type="submit" disabled={remove.isPending || code.trim().length === 0}>
            {remove.isPending ? 'Removing…' : 'Remove it'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}
