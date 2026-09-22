import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { removePharmacyItemMutation } from '../../services/api/generated/@tanstack/react-query.gen';
import type { PharmacyItem } from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';

const CODE_HEADER = 'X-Confirmation-Code';

// Taking a medicine off the register hides it from every list, so it asks for the confirmation
// code. The code is typed here and never stored; the API is what checks it.
export function RemoveMedicineDialog({
  item,
  onClose,
  onDone,
}: {
  item: PharmacyItem;
  onClose: () => void;
  onDone: () => void;
}) {
  const [code, setCode] = useState('');

  const remove = useMutation({
    ...removePharmacyItemMutation(),
    onSuccess: () => {
      toast.success(`${item.name} removed from the pharmacy register.`);
      onDone();
    },
    onError: () => setCode(''),
  });

  return (
    <Dialog title={`Remove ${item.name} from the register?`} onClose={onClose}>
      <p className="muted">
        Use this when the hospital no longer stocks it. It disappears from the pharmacy register
        and from every search. Its batches and movement history stay in the database for the
        record, and the name can be registered again later.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          remove.mutate({ path: { id: item.id }, headers: { [CODE_HEADER]: code.trim() } });
        }}
      >
        <label htmlFor="remove-medicine-code">Confirmation code</label>
        <input
          id="remove-medicine-code"
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
