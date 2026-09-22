import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  listEquipmentCategoriesForRemovalOptions,
  removeEquipmentCategoryMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type { EquipmentCategoryUsage } from '../../services/api/generated';
import { Dialog } from '../EquipmentPage';

const CODE_HEADER = 'X-Confirmation-Code';

// The hospital administrator tidies the category list here. Like Confirm new equipment, the code
// is kept in this component only - never stored - and the API checks it on every call.
export function RemoveCategoriesCard() {
  const queryClient = useQueryClient();
  const [code, setCode] = useState<string | null>(null);
  const [entered, setEntered] = useState('');
  const [unlocking, setUnlocking] = useState(false);
  const [removing, setRemoving] = useState<EquipmentCategoryUsage | null>(null);

  const headers = { [CODE_HEADER]: code ?? '' };

  const categories = useQuery({
    ...listEquipmentCategoriesForRemovalOptions({ headers }),
    enabled: code !== null,
  });

  function refresh() {
    queryClient.invalidateQueries({
      predicate: (query) => {
        const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
        return id === 'listEquipmentCategoriesForRemoval' || id === 'listEquipmentCategories';
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
        listEquipmentCategoriesForRemovalOptions({ headers: { [CODE_HEADER]: tried } }),
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
        'listEquipmentCategoriesForRemoval',
    });
  }

  if (code === null) {
    return (
      <div className="card">
        <h2>Remove categories</h2>
        <p className="muted" style={{ marginBottom: '0.9rem' }}>
          Take unwanted equipment categories off the list. Enter the confirmation code to see
          them.
        </p>

        <form onSubmit={unlock}>
          <div className="row">
            <div className="field">
              <label htmlFor="category-removal-code">Confirmation code</label>
              <input
                id="category-removal-code"
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

  const rows = categories.data ?? [];

  return (
    <div className="card">
      <div className="dialog-head">
        <h2>Remove categories</h2>
        <button type="button" className="secondary" onClick={lock}>
          Lock
        </button>
      </div>
      <p className="muted">
        A category can be removed once no item uses it. For one that still has items, remove
        those items first.
      </p>

      {categories.isPending && <p className="empty">Loading…</p>}

      {categories.isError && (
        <p className="empty">
          The list could not be loaded.{' '}
          <button type="button" className="secondary" onClick={lock}>
            Enter the code again
          </button>
        </p>
      )}

      {categories.isSuccess && rows.length === 0 && (
        <p className="empty">There are no categories.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Category</th>
              <th>Items using it</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((category) => (
              <tr key={category.id}>
                <td>{category.name}</td>
                <td>{category.item_count}</td>
                <td>
                  <button
                    type="button"
                    className="secondary danger"
                    disabled={category.item_count > 0}
                    title={
                      category.item_count > 0 ? 'Remove the items in it first' : undefined
                    }
                    onClick={() => setRemoving(category)}
                  >
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {removing && (
        <RemoveCategoryDialog
          category={removing}
          code={code}
          onClose={() => setRemoving(null)}
          onDone={() => {
            setRemoving(null);
            refresh();
          }}
        />
      )}
    </div>
  );
}

function RemoveCategoryDialog({
  category,
  code,
  onClose,
  onDone,
}: {
  category: EquipmentCategoryUsage;
  code: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const remove = useMutation({
    ...removeEquipmentCategoryMutation(),
    onSuccess: () => {
      toast.success(`${category.name} removed from the categories.`);
      onDone();
    },
  });

  return (
    <Dialog title={`Remove ${category.name}?`} onClose={onClose}>
      <p className="muted">
        It disappears from the category list and from the picker when registering equipment. The
        name can be added again later.
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={remove.isPending}
          onClick={() =>
            remove.mutate({ path: { id: category.id }, headers: { [CODE_HEADER]: code } })
          }
        >
          {remove.isPending ? 'Removing…' : 'Remove'}
        </button>
        <button type="button" className="secondary" onClick={onClose}>
          Cancel
        </button>
      </div>
    </Dialog>
  );
}
