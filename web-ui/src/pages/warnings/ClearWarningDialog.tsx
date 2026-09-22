import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { clearWarningMutation } from '../../services/api/generated/@tanstack/react-query.gen';
import type { Warning } from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';

const CODE_HEADER = 'X-Confirmation-Code';

// Done takes a resolved warning off the list for good, so it asks for the confirmation code. The
// code is typed here and never stored; the API is what checks it.
export function ClearWarningDialog({
  warning,
  onClose,
  onDone,
}: {
  warning: Warning;
  onClose: () => void;
  onDone: () => void;
}) {
  const [code, setCode] = useState('');

  const clear = useMutation({
    ...clearWarningMutation(),
    onSuccess: () => {
      toast.success('Marked done. It has left the list.');
      onDone();
    },
    onError: () => setCode(''),
  });

  return (
    <Dialog title="Mark this warning done?" onClose={onClose}>
      <p className="muted">
        {warning.related_entity_label ?? 'This warning'}: {warning.recommended_action}
      </p>
      <p className="muted">
        The problem is already fixed. Marking it done takes it off the Resolved list. It stays in
        the database for the record.
      </p>

      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          clear.mutate({ path: { id: warning.id }, headers: { [CODE_HEADER]: code.trim() } });
        }}
      >
        <label htmlFor="clear-warning-code">Confirmation code</label>
        <input
          id="clear-warning-code"
          type="password"
          autoComplete="off"
          value={code}
          disabled={clear.isPending}
          onChange={(event) => setCode(event.target.value)}
        />

        <div className="actions">
          <button type="submit" disabled={clear.isPending || code.trim().length === 0}>
            {clear.isPending ? 'Saving…' : 'Done'}
          </button>
          <button type="button" className="secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
    </Dialog>
  );
}
