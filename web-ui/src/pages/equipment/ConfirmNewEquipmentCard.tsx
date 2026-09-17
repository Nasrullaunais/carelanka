import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  confirmEquipmentItemMutation,
  listEquipmentItemsAwaitingConfirmationOptions,
  rejectEquipmentItemMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type { EquipmentItem } from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';

const CODE_HEADER = 'X-Confirmation-Code';

// The same queue the hospital administrator works through in the mobile app. The code is kept in
// this component only - never stored - and the API checks it on every call.
export function ConfirmNewEquipmentCard() {
  const queryClient = useQueryClient();
  const [code, setCode] = useState<string | null>(null);
  const [entered, setEntered] = useState('');
  const [unlocking, setUnlocking] = useState(false);
  const [rejecting, setRejecting] = useState<EquipmentItem | null>(null);

  const headers = { [CODE_HEADER]: code ?? '' };

  const pending = useQuery({
    ...listEquipmentItemsAwaitingConfirmationOptions({ headers }),
    enabled: code !== null,
  });

  function refresh() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return (
          id === 'listEquipmentItemsAwaitingConfirmation' ||
          id === 'countEquipmentItemsAwaitingConfirmation' ||
          id === 'listEquipmentItems'
        );
      },
    });
  }

  async function unlock(event: FormEvent) {
    event.preventDefault();
    const tried = entered.trim();
    if (tried.length === 0) return;

    setUnlocking(true);

    try {
      // Asked once before unlocking, so a wrong code leaves the card locked. The failure itself
      // is toasted by the transport with the server's message.
      await queryClient.fetchQuery(
        listEquipmentItemsAwaitingConfirmationOptions({ headers: { [CODE_HEADER]: tried } }),
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
        'listEquipmentItemsAwaitingConfirmation',
    });
  }

  const confirm = useMutation({
    ...confirmEquipmentItemMutation(),
    onSuccess: (item) => {
      toast.success(`${item.name} (${item.asset_tag}) confirmed and added to the register.`);
      refresh();
    },
  });

  if (code === null) {
    return (
      <div className="card">
        <h2>Confirm new equipment</h2>
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          Items the equipment manager registers wait here until you confirm them. Enter the
          confirmation code to see them.
        </p>

        <form onSubmit={unlock}>
          <div className="row">
            <div className="field">
              <label htmlFor="confirmation-code">Confirmation code</label>
              <input
                id="confirmation-code"
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

  const rows = pending.data ?? [];

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>Confirm new equipment</h2>
        <button type="button" className="secondary" onClick={lock}>
          Lock
        </button>
      </div>

      {pending.isPending && <p className="empty">Loading…</p>}

      {pending.isError && (
        <p className="empty">
          The list could not be loaded.{' '}
          <button type="button" className="secondary" onClick={lock}>
            Enter the code again
          </button>
        </p>
      )}

      {pending.isSuccess && rows.length === 0 && (
        <p className="empty">Nothing is waiting. Every registered item has been dealt with.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Registered</th>
              <th>Item</th>
              <th>Category</th>
              <th>Make</th>
              <th>Serial</th>
              <th>Location</th>
              <th>Purchased</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((item) => (
              <tr key={item.id}>
                <td>{new Date(item.created_at).toLocaleString()}</td>
                <td>
                  {item.name}
                  <br />
                  <span className="muted">{item.asset_tag}</span>
                </td>
                <td>{item.category_name}</td>
                <td>
                  {item.manufacturer} {item.model}
                </td>
                <td>{item.serial_number ?? <span className="muted">—</span>}</td>
                <td>{item.ward_name ?? 'Central store'}</td>
                <td>{item.purchase_date}</td>
                <td>
                  <button
                    type="button"
                    disabled={confirm.isPending}
                    onClick={() => confirm.mutate({ path: { id: item.id }, headers })}
                  >
                    Confirm
                  </button>{' '}
                  <button type="button" className="secondary" onClick={() => setRejecting(item)}>
                    Reject
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {rejecting && (
        <RejectDialog
          item={rejecting}
          code={code}
          onClose={() => setRejecting(null)}
          onDone={() => {
            setRejecting(null);
            refresh();
          }}
        />
      )}
    </div>
  );
}

function RejectDialog({
  item,
  code,
  onClose,
  onDone,
}: {
  item: EquipmentItem;
  code: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const reject = useMutation({
    ...rejectEquipmentItemMutation(),
    onSuccess: () => {
      toast.success(`${item.name} rejected. ${item.asset_tag} can be registered again.`);
      onDone();
    },
  });

  return (
    <Dialog title={`Reject ${item.name}?`} onClose={onClose}>
      <p className="muted">
        It is removed and never reaches the register. The equipment manager can register{' '}
        {item.asset_tag} again with the right details.
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={reject.isPending}
          onClick={() => reject.mutate({ path: { id: item.id }, headers: { [CODE_HEADER]: code } })}
        >
          {reject.isPending ? 'Rejecting…' : 'Reject'}
        </button>
        <button type="button" className="secondary" onClick={onClose}>
          Cancel
        </button>
      </div>
    </Dialog>
  );
}
